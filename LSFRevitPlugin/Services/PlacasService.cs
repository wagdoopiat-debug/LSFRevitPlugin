using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>Tipo, medida e orientacao das placas de uma face.</summary>
    public sealed class EscolhaPlaca
    {
        public TipoPlaca Tipo { get; set; }
        public MedidaPlaca Medida { get; set; }
        public OrientacaoPlaca Orientacao { get; set; }

        /// <summary>Usa a primeira medida disponivel do tipo (a de fabrica 1,20 x 2,40 m).</summary>
        public static EscolhaPlaca Criar(TipoPlaca tipo, OrientacaoPlaca orientacao)
        {
            return new EscolhaPlaca
            {
                Tipo = tipo,
                Medida = PlacaCatalogo.Padrao.MedidasDisponiveis(tipo).First(),
                Orientacao = orientacao
            };
        }
    }

    public sealed class ResumoPlacas
    {
        public int Paredes { get; set; }
        public int Paineis { get; set; }
        public int PaineisRefeitos { get; set; }
        public int PaineisMantidos { get; set; }
        public int Pecas { get; set; }
        public int PecasComRecorte { get; set; }
        public int PecasReaproveitadas { get; set; }
        public int PecasMantidas { get; set; }
        public int PecasComFalha { get; set; }

        public List<string> LinhasPorEstoque { get; } = new List<string>();
        public List<string> Avisos { get; } = new List<string>();
    }

    /// <summary>
    /// Cria as placas em 3D (um DirectShape por peca, com a cor do tipo) a partir da paginacao.
    /// Em tres passos: (1) desenha as pecas de todas as faces de todos os paineis; (2) distribui
    /// as pecas do projeto inteiro sobre as placas e sobras, da maior para a menor, para
    /// aproveitar o maximo; (3) cria os elementos dos paineis marcados para refazer e atualiza o
    /// texto das placas dos paineis mantidos.
    /// Nas pontas da parede a placa acompanha os encontros em L e T e, no meio, as paredes que
    /// encostam (T visto pela parede que passa) ou cruzam (+) interrompem a placa (ver EncontrosPlacas).
    /// Na geometria 3D, placas vizinhas (e as de paineis vizinhos) ficam separadas por uma junta
    /// visivel de FolgaJuntaMm; medidas, codigos e quantitativo continuam nominais.
    /// </summary>
    public static class PlacasService
    {
        /// <summary>Texto que abre o Comentario de toda placa gerada (diferente do prefixo da estrutura).</summary>
        public const string PrefixoEtiqueta = "LSF-PLACA | ";

        /// <summary>Largura da junta visivel entre placas vizinhas, so na geometria 3D (cada peca cede metade).</summary>
        public const double FolgaJuntaMm = 3.0;

        /// <summary>Uma face de um painel ja desenhada, esperando a distribuicao das pecas.</summary>
        private sealed class TrabalhoDeFace
        {
            public LeituraParede Leitura;
            public PainelInfo Painel;
            public int NumeroFace;
            public EscolhaPlaca Escolha;
            public double InicioPlacaMm;
            public double LarguraFaceMm;
            public bool InicioEhJuntaDePainel;
            public bool FimEhJuntaDePainel;
            public bool Refaz;
            public List<PlacaExistente> DoPainel;
            public ResultadoPaginacao Resultado;
            public string OrigemAviso;
        }

        /// <summary>Quanto cada lado de uma peca cede para formar a junta visivel (0 = lado sem junta).</summary>
        private sealed class FolgasDaPeca
        {
            public double U0;
            public double U1;
            public double Z0;
            public double Z1;
        }

        /// <param name="existentes">As placas que ja existem no projeto.</param>
        /// <param name="escolhaDaFace">Tipo, medida e orientacao de uma face (1 ou 2) de um painel a refazer.</param>
        /// <param name="refazer">true se o painel deve ter as placas refeitas.</param>
        public static ResumoPlacas Gerar(
            Document doc, IList<LeituraParede> paredes, PlacasExistentes existentes,
            Func<LeituraParede, PainelInfo, int, EscolhaPlaca> escolhaDaFace,
            Func<LeituraParede, PainelInfo, bool> refazer)
        {
            var resumo = new ResumoPlacas();
            var estoques = new ColecaoDeEstoques();
            var materiais = new Dictionary<TipoPlaca, ElementId>();
            var trabalhos = new List<TrabalhoDeFace>();

            using (Transaction transacao = new Transaction(doc, "LSF - Gerar placas"))
            {
                transacao.Start();

                // ---- Passo 1: desenha as pecas de todas as faces ----
                foreach (LeituraParede leitura in paredes)
                {
                    if (!leitura.TemEstrutura)
                    {
                        resumo.Avisos.Add("Parede " + leitura.Parede.Id + ": sem estrutura gerada, ignorada.");
                        continue;
                    }

                    resumo.Paredes++;

                    if (leitura.PainelizacaoDesatualizada)
                    {
                        resumo.Avisos.Add(
                            "Parede " + leitura.Parede.Id
                            + ": painelizacao desatualizada (o comprimento mudou); tratada como painel unico. Painelize e gere a estrutura de novo.");
                    }

                    int paredeId = leitura.Parede.Id.IntegerValue;
                    int numeroPrimeiroPainel = leitura.Paineis[0].Numero;
                    int numeroUltimoPainel = leitura.Paineis[leitura.Paineis.Count - 1].Numero;

                    foreach (PainelInfo painel in leitura.Paineis)
                    {
                        bool refaz = refazer(leitura, painel);
                        List<PlacaExistente> doPainel = existentes.DoPainel(paredeId, painel.Numero);

                        // Painel sem placas e nao marcado: nao entra.
                        if (!refaz && doPainel.Count == 0) continue;

                        resumo.Paineis++;

                        if (refaz)
                        {
                            resumo.PaineisRefeitos++;
                            if (doPainel.Count > 0)
                            {
                                doc.Delete(doPainel.Select(p => p.Elemento.Id).ToList());
                            }
                        }
                        else
                        {
                            resumo.PaineisMantidos++;
                        }

                        double inicioPainelMm = ParaMm(painel.InicioInterno);
                        double fimPainelMm = ParaMm(painel.FimInterno);

                        for (int numeroFace = 1; numeroFace <= 2; numeroFace++)
                        {
                            string origemAviso = "Parede " + paredeId + ", painel " + painel.Numero
                                + ", face " + numeroFace + ": ";

                            EscolhaPlaca escolha = refaz
                                ? escolhaDaFace(leitura, painel, numeroFace)
                                : existentes.Escolha(paredeId, painel.Numero, numeroFace);

                            if (escolha == null)
                            {
                                resumo.Avisos.Add(origemAviso + "sem dados das placas ja geradas; marque Refazer neste painel.");
                                continue;
                            }

                            // Nas pontas da parede a placa acompanha o encontro (L ou T); painel
                            // do meio nao muda. Paredes que encostam ou cruzam interrompem a placa.
                            double espessuraMm = PlacaCatalogo.RegraDoTipo(escolha.Tipo).EspessuraPadraoMm;

                            bool ehPrimeiro = painel.Numero == numeroPrimeiroPainel;
                            bool ehUltimo = painel.Numero == numeroUltimoPainel;

                            double inicioPlacaMm = ehPrimeiro
                                ? EncontrosPlacas.PosicaoDoExtremo(leitura, paredes, true, numeroFace, espessuraMm)
                                : inicioPainelMm;
                            double fimPlacaMm = ehUltimo
                                ? EncontrosPlacas.PosicaoDoExtremo(leitura, paredes, false, numeroFace, espessuraMm)
                                : fimPainelMm;

                            // Canto ou T na ponta final: as placas tambem sao assentadas pelo fim,
                            // para o canto ficar com placa grande.
                            bool ancorarNoFim = ehUltimo
                                && EncontrosPlacas.TemEncontroNaPonta(leitura, paredes, false);

                            List<RetanguloMm> interrupcoes = EncontrosPlacas.Interrupcoes(
                                leitura, paredes, numeroFace, espessuraMm);

                            ResultadoPaginacao resultado;
                            try
                            {
                                EntradaPaginacao entrada = PreparoEntradaPaginacao.Criar(
                                    painel.Numero, numeroFace, inicioPlacaMm, fimPlacaMm,
                                    leitura.AlturaMm, leitura.MontantesMm, leitura.AberturasMm,
                                    escolha.Tipo, escolha.Medida, escolha.Orientacao, interrupcoes);
                                entrada.PrefixoCodigo = paredeId + "-P" + painel.Numero;
                                entrada.AncorarNoFim = ancorarNoFim;

                                resultado = Paginador.PaginarSemEstoque(entrada);
                            }
                            catch (ArgumentException ex)
                            {
                                resumo.Avisos.Add(origemAviso + ex.Message);
                                continue;
                            }

                            trabalhos.Add(new TrabalhoDeFace
                            {
                                Leitura = leitura,
                                Painel = painel,
                                NumeroFace = numeroFace,
                                Escolha = escolha,
                                InicioPlacaMm = inicioPlacaMm,
                                LarguraFaceMm = fimPlacaMm - inicioPlacaMm,
                                InicioEhJuntaDePainel = !ehPrimeiro,
                                FimEhJuntaDePainel = !ehUltimo,
                                Refaz = refaz,
                                DoPainel = doPainel,
                                Resultado = resultado,
                                OrigemAviso = origemAviso
                            });
                        }
                    }
                }

                // ---- Passo 2: distribui as pecas do projeto inteiro, um estoque por tipo e medida ----
                foreach (IGrouping<EstoquePlacas, TrabalhoDeFace> grupo in trabalhos.GroupBy(
                    w => estoques.Obter(w.Escolha.Tipo, w.Escolha.Medida)))
                {
                    Paginador.Alocar(grupo.Select(w => w.Resultado), grupo.Key);
                }

                // ---- Passo 3: cria os elementos ou atualiza o texto das placas mantidas ----
                foreach (TrabalhoDeFace trabalho in trabalhos)
                {
                    foreach (string aviso in trabalho.Resultado.Avisos)
                    {
                        resumo.Avisos.Add(trabalho.OrigemAviso + aviso);
                    }

                    if (trabalho.Refaz)
                    {
                        ElementId materialId;
                        if (!materiais.TryGetValue(trabalho.Escolha.Tipo, out materialId))
                        {
                            materialId = PlacaMateriais.Obter(doc, trabalho.Escolha.Tipo);
                            materiais[trabalho.Escolha.Tipo] = materialId;
                        }

                        CriarPecas(doc, trabalho, materialId, resumo);
                    }
                    else
                    {
                        AtualizarMantidas(trabalho.Leitura, trabalho.Painel, trabalho.NumeroFace,
                            trabalho.Escolha, trabalho.Resultado, trabalho.DoPainel,
                            resumo, trabalho.OrigemAviso);
                    }
                }

                transacao.Commit();
            }

            foreach (EstoquePlacas estoque in estoques.Todos)
            {
                resumo.LinhasPorEstoque.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} {1}: {2} placas usadas, sobras guardadas {3:0.00} m2",
                    PlacaCatalogo.NomeDoTipo(estoque.Tipo),
                    estoque.Medida.Descricao,
                    estoque.PlacasUsadas,
                    estoque.AreaDeSobrasMm2 / 1000000.0));
            }

            return resumo;
        }

        /// <summary>
        /// Painel que nao foi refeito: as placas ficam como estao, mas o texto delas (placa de
        /// origem, sobra aproveitada) e atualizado, porque as sobras do projeto podem ter mudado.
        /// </summary>
        private static void AtualizarMantidas(
            LeituraParede leitura, PainelInfo painel, int numeroFace, EscolhaPlaca escolha,
            ResultadoPaginacao resultado, List<PlacaExistente> doPainel, ResumoPlacas resumo, string origemAviso)
        {
            List<PlacaExistente> daFace = doPainel.Where(p => p.Face == numeroFace).ToList();
            int naoEncontradas = 0;

            foreach (PecaPlaca peca in resultado.Pecas)
            {
                PlacaExistente existente = daFace.FirstOrDefault(p => p.Codigo == peca.Codigo);
                if (existente == null)
                {
                    naoEncontradas++;
                    continue;
                }

                Parameter comentario = existente.Elemento.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null && !comentario.IsReadOnly)
                {
                    comentario.Set(MontarComentario(leitura, painel, numeroFace, escolha, peca));
                }

                resumo.PecasMantidas++;
            }

            if (naoEncontradas > 0 || daFace.Count != resultado.Pecas.Count)
            {
                resumo.Avisos.Add(origemAviso
                    + "as placas existentes nao batem com a paginacao atual (a estrutura, os vaos ou os encontros mudaram?). Marque Refazer neste painel.");
            }
        }

        private static void CriarPecas(
            Document doc, TrabalhoDeFace trabalho, ElementId materialId, ResumoPlacas resumo)
        {
            LeituraParede leitura = trabalho.Leitura;
            PainelInfo painel = trabalho.Painel;
            int numeroFace = trabalho.NumeroFace;
            EscolhaPlaca escolha = trabalho.Escolha;
            ResultadoPaginacao resultado = trabalho.Resultado;
            double inicioPlacaMm = trabalho.InicioPlacaMm;
            string origemAviso = trabalho.OrigemAviso;

            RegraFixacaoPlaca regra = PlacaCatalogo.RegraDoTipo(escolha.Tipo);
            double espessuraMm = regra.EspessuraPadraoMm;

            // Face 1 fica do lado da Normal da parede, encostada na estrutura; a face 2 do lado oposto.
            double n0Mm = numeroFace == 1 ? leitura.EstruturaNMaxMm : leitura.EstruturaNMinMm;
            int sinal = numeroFace == 1 ? 1 : -1;

            List<PecaPlaca> todasAsPecas = resultado.Pecas.ToList();

            foreach (PecaPlaca peca in todasAsPecas)
            {
                try
                {
                    FolgasDaPeca folgas = CalcularFolgas(
                        peca, todasAsPecas, trabalho.InicioEhJuntaDePainel,
                        trabalho.FimEhJuntaDePainel, trabalho.LarguraFaceMm);

                    bool usouCelulas;
                    List<GeometryObject> solidos = CriarSolidosDaPeca(
                        leitura, inicioPlacaMm, peca, folgas, n0Mm, espessuraMm, sinal, materialId, out usouCelulas);

                    if (usouCelulas)
                    {
                        resumo.Avisos.Add(origemAviso + "placa " + peca.Codigo
                            + ": o recorte do solido falhou e a peca foi criada por celulas (podem aparecer linhas de emenda dentro dela).");
                    }

                    DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.SetShape(solidos);
                    ds.Name = peca.Codigo;

                    Parameter marca = ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (marca != null && !marca.IsReadOnly) marca.Set(peca.Codigo);

                    Parameter comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comentario != null && !comentario.IsReadOnly)
                    {
                        comentario.Set(MontarComentario(leitura, painel, numeroFace, escolha, peca));
                    }

                    resumo.Pecas++;
                    if (peca.Recortes.Count > 0) resumo.PecasComRecorte++;
                    if (peca.Reaproveitada) resumo.PecasReaproveitadas++;
                }
                catch (Exception ex)
                {
                    resumo.PecasComFalha++;
                    resumo.Avisos.Add(origemAviso + "falha ao criar a placa " + peca.Codigo + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Define quais lados da peca cedem metade da junta: os que encostam em outra peca da mesma
        /// face e, quando a ponta da face e divisa com outro painel, esse lado tambem.
        /// Pontas da parede, piso, topo e aberturas ficam sem desconto.
        /// </summary>
        private static FolgasDaPeca CalcularFolgas(
            PecaPlaca peca, List<PecaPlaca> todas, bool inicioEhJuntaDePainel,
            bool fimEhJuntaDePainel, double larguraFaceMm)
        {
            const double eps = 0.5;
            double meia = FolgaJuntaMm / 2.0;

            var folgas = new FolgasDaPeca();
            RetanguloMm a = peca.Contorno;

            if (inicioEhJuntaDePainel && Math.Abs(a.U0) <= eps) folgas.U0 = meia;
            if (fimEhJuntaDePainel && Math.Abs(a.U1 - larguraFaceMm) <= eps) folgas.U1 = meia;

            foreach (PecaPlaca outra in todas)
            {
                if (ReferenceEquals(outra, peca)) continue;

                RetanguloMm b = outra.Contorno;

                bool sobrepoeZ = Math.Min(a.Z1, b.Z1) - Math.Max(a.Z0, b.Z0) > eps;
                bool sobrepoeU = Math.Min(a.U1, b.U1) - Math.Max(a.U0, b.U0) > eps;

                if (sobrepoeZ && Math.Abs(a.U1 - b.U0) <= eps) folgas.U1 = meia;
                if (sobrepoeZ && Math.Abs(a.U0 - b.U1) <= eps) folgas.U0 = meia;
                if (sobrepoeU && Math.Abs(a.Z1 - b.Z0) <= eps) folgas.Z1 = meia;
                if (sobrepoeU && Math.Abs(a.Z0 - b.Z1) <= eps) folgas.Z0 = meia;
            }

            return folgas;
        }

        /// <summary>
        /// Encolhe um retangulo (o contorno ou uma celula dele) nos lados que coincidem com um lado
        /// do contorno que cede folga.
        /// </summary>
        private static RetanguloMm Encolher(RetanguloMm r, RetanguloMm contorno, FolgasDaPeca folgas)
        {
            const double eps = 0.5;

            double u0 = Math.Abs(r.U0 - contorno.U0) <= eps ? r.U0 + folgas.U0 : r.U0;
            double u1 = Math.Abs(r.U1 - contorno.U1) <= eps ? r.U1 - folgas.U1 : r.U1;
            double z0 = Math.Abs(r.Z0 - contorno.Z0) <= eps ? r.Z0 + folgas.Z0 : r.Z0;
            double z1 = Math.Abs(r.Z1 - contorno.Z1) <= eps ? r.Z1 - folgas.Z1 : r.Z1;

            return new RetanguloMm(u0, u1, z0, z1);
        }

        /// <summary>
        /// A peca como UM solido: a caixa do contorno (ja com a folga das juntas) menos os recortes.
        /// Assim uma peca em L nao tem linha de emenda no meio. Se o recorte falhar, cai para um
        /// solido por celula e avisa por usouCelulas.
        /// </summary>
        private static List<GeometryObject> CriarSolidosDaPeca(
            LeituraParede leitura, double inicioPlacaMm, PecaPlaca peca, FolgasDaPeca folgas,
            double n0Mm, double espessuraMm, int sinal, ElementId materialId, out bool usouCelulas)
        {
            usouCelulas = false;
            var lista = new List<GeometryObject>();

            try
            {
                RetanguloMm contornoComFolga = Encolher(peca.Contorno, peca.Contorno, folgas);
                Solid unico = CriarCaixa(leitura, inicioPlacaMm, contornoComFolga, n0Mm, espessuraMm, sinal, materialId);

                foreach (RetanguloMm recorte in peca.Recortes)
                {
                    // O solido de corte e 1 mm mais fundo nas duas faces e passa 1 mm alem da borda
                    // (ja descontada a folga) onde o recorte encosta no contorno, para o corte sair limpo.
                    RetanguloMm ampliado = AmpliarNasBordas(recorte, peca.Contorno, 1.0 + FolgaJuntaMm / 2.0);
                    Solid corte = CriarCaixa(
                        leitura, inicioPlacaMm, ampliado, n0Mm - sinal * 1.0, espessuraMm + 2.0, sinal, materialId);

                    unico = BooleanOperationsUtils.ExecuteBooleanOperation(unico, corte, BooleanOperationsType.Difference);
                }

                if (unico != null && unico.Volume > 0)
                {
                    lista.Add(unico);
                    return lista;
                }
            }
            catch (Exception)
            {
                // Cai para o solido por celula abaixo.
            }

            usouCelulas = true;
            lista.Clear();
            foreach (RetanguloMm celula in peca.Celulas)
            {
                RetanguloMm celulaComFolga = Encolher(celula, peca.Contorno, folgas);
                lista.Add(CriarCaixa(leitura, inicioPlacaMm, celulaComFolga, n0Mm, espessuraMm, sinal, materialId));
            }

            return lista;
        }

        private static RetanguloMm AmpliarNasBordas(RetanguloMm recorte, RetanguloMm contorno, double margemMm)
        {
            const double eps = 0.5;

            return new RetanguloMm(
                Math.Abs(recorte.U0 - contorno.U0) <= eps ? recorte.U0 - margemMm : recorte.U0,
                Math.Abs(recorte.U1 - contorno.U1) <= eps ? recorte.U1 + margemMm : recorte.U1,
                Math.Abs(recorte.Z0 - contorno.Z0) <= eps ? recorte.Z0 - margemMm : recorte.Z0,
                Math.Abs(recorte.Z1 - contorno.Z1) <= eps ? recorte.Z1 + margemMm : recorte.Z1);
        }

        /// <summary>
        /// Texto do campo Comentarios de uma placa. A ordem dos primeiros 7 campos e fixa; os
        /// demais sao reconhecidos pelo inicio (placa, medida, orientacao, area, recortes) em
        /// PlacasExistentes.
        /// </summary>
        private static string MontarComentario(
            LeituraParede leitura, PainelInfo painel, int numeroFace, EscolhaPlaca escolha, PecaPlaca peca)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;

            string texto = PrefixoEtiqueta
                + "Parede " + leitura.Parede.Id.IntegerValue.ToString(inv)
                + " | Painel " + painel.Numero.ToString(inv)
                + " | Face " + numeroFace.ToString(inv)
                + " | " + PlacaCatalogo.NomeDoTipo(escolha.Tipo)
                + " | " + peca.Codigo
                + " | " + peca.Contorno.Largura.ToString("0", inv) + " x " + peca.Contorno.Altura.ToString("0", inv) + " mm"
                + " | placa " + peca.PlacaOrigem
                + (peca.Reaproveitada ? " (sobra de " + peca.OrigemDaSobra + ")" : "")
                + " | medida " + escolha.Medida.LadoCurtoMm.ToString("0", inv) + "x" + escolha.Medida.LadoLongoMm.ToString("0", inv)
                + " | orientacao " + escolha.Orientacao
                + " | area " + Math.Round(peca.AreaMm2).ToString("0", inv);

            for (int i = 0; i < peca.Recortes.Count; i++)
            {
                RetanguloMm r = peca.Recortes[i];
                texto += " | " + peca.CodigoDoRecorte(i) + " "
                    + r.Largura.ToString("0", inv) + " x " + r.Altura.ToString("0", inv) + " mm";
            }

            return texto;
        }

        /// <summary>
        /// Uma caixa de placa: o retangulo no plano da parede, extrudado para fora da
        /// estrutura pela espessura da placa.
        /// </summary>
        private static Solid CriarCaixa(
            LeituraParede leitura, double inicioPlacaMm, RetanguloMm retangulo,
            double n0Mm, double espessuraMm, int sinal, ElementId materialId)
        {
            XYZ origem = leitura.Inicio + leitura.Normal.Multiply(Interno(n0Mm));

            Func<double, double, XYZ> ponto = (uMm, zMm) =>
                origem
                + leitura.Direcao.Multiply(Interno(inicioPlacaMm + uMm))
                + XYZ.BasisZ.Multiply(Interno(zMm));

            XYZ a = ponto(retangulo.U0, retangulo.Z0);
            XYZ b = ponto(retangulo.U1, retangulo.Z0);
            XYZ c = ponto(retangulo.U1, retangulo.Z1);
            XYZ d = ponto(retangulo.U0, retangulo.Z1);

            var contorno = new CurveLoop();
            contorno.Append(Line.CreateBound(a, b));
            contorno.Append(Line.CreateBound(b, c));
            contorno.Append(Line.CreateBound(c, d));
            contorno.Append(Line.CreateBound(d, a));

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { contorno },
                leitura.Normal.Multiply(sinal),
                Interno(espessuraMm),
                new SolidOptions(materialId, ElementId.InvalidElementId));
        }

        private static double Interno(double mm)
        {
            return RevitVersionAdapter.MilimetrosParaInterno(mm);
        }

        private static double ParaMm(double valorInterno)
        {
            return RevitVersionAdapter.InternoParaCentimetros(valorInterno) * 10.0;
        }
    }
}