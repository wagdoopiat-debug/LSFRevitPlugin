using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;

namespace LSFRevitPlugin.Services
{
    /// <summary>Um parafuso: onde fica a cabeca, para onde ele entra e de que ligacao ele e.</summary>
    public sealed class ParafusoPosicao
    {
        /// <summary>Ponto da cabeca (unidades internas do Revit).</summary>
        public XYZ Ponto { get; set; }

        /// <summary>Direcao em que o parafuso entra (vetor unitario).</summary>
        public XYZ Direcao { get; set; }

        public double ComprimentoMm { get; set; }
        public string Categoria { get; set; }
        public int ParedeId { get; set; }
        public int Painel { get; set; }

        /// <summary>Codigo da peca de placa (so nos parafusos de placa).</summary>
        public string Peca { get; set; }

        /// <summary>Tipo da placa (so nos parafusos de placa).</summary>
        public string Tipo { get; set; }

        /// <summary>Descricao do parafuso.</summary>
        public string Descricao { get; set; }
    }

    /// <summary>Par de montantes que se tocam e os parafusos que os unem.</summary>
    public sealed class LigacaoMontantes
    {
        public string Tipo { get; set; }
        public ElementoEstruturaLido A { get; set; }
        public ElementoEstruturaLido B { get; set; }

        /// <summary>O montante (do par) de menor parede/painel; a ligacao e contada nele.</summary>
        public ElementoEstruturaLido Dono { get; set; }

        public double AlturaMm { get; set; }
        public int Parafusos { get; set; }
    }

    public sealed class ResultadoParafusos
    {
        public List<ParafusoPosicao> Parafusos { get; } = new List<ParafusoPosicao>();
        public List<LigacaoMontantes> Ligacoes { get; } = new List<LigacaoMontantes>();

        /// <summary>Quantidade de montantes verticais que recebem parafusos na guia.</summary>
        public int Montantes { get; set; }
    }

    /// <summary>Resultado da criacao dos parafusos no 3D.</summary>
    public sealed class ResultadoCriacao3D
    {
        /// <summary>Parafusos criados.</summary>
        public int Criados { get; set; }

        /// <summary>Parafusos que nao puderam ser criados.</summary>
        public int Falhas { get; set; }

        /// <summary>Mensagem do primeiro erro, se houve.</summary>
        public string PrimeiroErro { get; set; }

        /// <summary>Elementos 3D criados (um por categoria, parede e painel).</summary>
        public int Elementos { get; set; }

        /// <summary>Elementos 3D de parafusos antigos que foram apagados antes.</summary>
        public int Apagados { get; set; }
    }

    /// <summary>
    /// Calcula a posicao de cada parafuso da estrutura (montante na guia, emendas costas com costas e
    /// conexoes entre paineis e entre paredes) e das placas, e cria os parafusos no 3D.
    /// </summary>
    public static class ParafusosService
    {
        public const string CategoriaGuia = "Montante na guia";
        public const string CategoriaCostas = "Emenda costas com costas";
        public const string CategoriaPaineis = "Conexao entre paineis";
        public const string CategoriaParedes = "Conexao entre paredes";
        public const string CategoriaPlaca = "Placa";

        public const string NomeParafusoEstrutura = "Autobrocante lentilha #8/#10";
        public const int ParafusosPorMontanteNaGuia = 4;

        /// <summary>Passo do parafuso nas emendas e conexoes (faixa de projeto: 200 a 300 mm).</summary>
        public const double PassoEmendaMm = 250.0;

        /// <summary>Distancia do parafuso a borda nas emendas e conexoes (faixa de projeto: 10 a 15 mm).</summary>
        public const double BordaEmendaMm = 12.5;

        /// <summary>Texto que abre o campo Comentarios dos elementos 3D de parafusos.</summary>
        public const string PrefixoEtiqueta = "LSF-PARAFUSOS | ";

        // Parafuso da estrutura (so para o desenho 3D)
        private const double DistanciaDaPontaMm = 15.0;
        private const double ComprimentoParafusoEstruturaMm = 13.0;

        // Desenho 3D do parafuso: corpo que entra no material e cabeca que fica para fora da face
        private const double RaioDoCorpoMm = 2.0;
        private const double RaioDaCabecaMm = 4.0;
        private const double AlturaDaCabecaMm = 3.0;

        // Montantes que se tocam
        private const double ToleranciaContatoMm = 2.0;
        private const double SobreposicaoMinimaMm = 5.0;
        private const double AlturaMinimaContatoMm = 100.0;
        private const double RaioBuscaMm = 1500.0;

        // Parafusos das placas
        private const double AlcanceDaGuiaMm = 60.0;
        private const double PassoBuscaLinhaMm = 3.0;
        private const double DistanciaMinimaEntreLinhasMm = 20.0;
        private const double DistanciaMinimaDaLinhaVerticalMm = 30.0;

        // ------------------------------------------------------------------------------------
        // Calculo
        // ------------------------------------------------------------------------------------

        internal static ResultadoParafusos Calcular(
            Document doc, List<ElementoEstruturaLido> estrutura, List<PlacaExistente> placas, List<string> avisos)
        {
            var resultado = new ResultadoParafusos();

            List<ElementoEstruturaLido> montantes = estrutura.Where(e => e.EhMontanteVertical).ToList();
            resultado.Montantes = montantes.Count;

            foreach (ElementoEstruturaLido montante in montantes)
            {
                AdicionarMontanteNaGuia(resultado, montante);
            }

            AdicionarLigacoes(resultado, montantes);
            AdicionarPlacas(doc, resultado, estrutura, placas, avisos);

            return resultado;
        }

        /// <summary>4 parafusos por montante: 2 em cada ponta (um em cada mesa), entrando pelas mesas da guia.</summary>
        private static void AdicionarMontanteNaGuia(ResultadoParafusos resultado, ElementoEstruturaLido elemento)
        {
            MedidaElemento m = elemento.Medida;

            bool atravessaEmN = (m.N1Mm - m.N0Mm) >= (m.U1Mm - m.U0Mm);
            double uCentro = (m.U0Mm + m.U1Mm) / 2.0;
            double nCentro = (m.N0Mm + m.N1Mm) / 2.0;

            foreach (double z in new[] { m.Z0Mm + DistanciaDaPontaMm, m.Z1Mm - DistanciaDaPontaMm })
            {
                foreach (int lado in new[] { 1, -1 })
                {
                    XYZ ponto;
                    XYZ direcao;

                    if (atravessaEmN)
                    {
                        ponto = Ponto(m, uCentro, lado > 0 ? m.N1Mm : m.N0Mm, z);
                        direcao = m.Nor.Multiply(-lado);
                    }
                    else
                    {
                        ponto = Ponto(m, lado > 0 ? m.U1Mm : m.U0Mm, nCentro, z);
                        direcao = m.Dir.Multiply(-lado);
                    }

                    resultado.Parafusos.Add(new ParafusoPosicao
                    {
                        Ponto = ponto,
                        Direcao = direcao,
                        ComprimentoMm = ComprimentoParafusoEstruturaMm,
                        Categoria = CategoriaGuia,
                        ParedeId = elemento.ParedeId,
                        Painel = elemento.Painel,
                        Descricao = NomeParafusoEstrutura
                    });
                }
            }
        }

        /// <summary>
        /// Pares de montantes verticais que se tocam, com parafusos em zigue-zague ao longo da altura
        /// de contato. A caixa de cada montante e medida nos eixos da parede dele; os dois testes
        /// (nos eixos de A e nos de B) precisam concordar.
        /// </summary>
        private static void AdicionarLigacoes(ResultadoParafusos resultado, List<ElementoEstruturaLido> montantes)
        {
            double raio2 = RaioBuscaMm * RaioBuscaMm;

            for (int i = 0; i < montantes.Count; i++)
            {
                ElementoEstruturaLido a = montantes[i];
                MedidaElemento ma = a.Medida;

                for (int j = i + 1; j < montantes.Count; j++)
                {
                    ElementoEstruturaLido b = montantes[j];
                    MedidaElemento mb = b.Medida;

                    double dx = ma.CentroXMm - mb.CentroXMm;
                    double dy = ma.CentroYMm - mb.CentroYMm;
                    if (dx * dx + dy * dy > raio2) continue;

                    double zIni = Math.Max(ma.Z0Mm, mb.Z0Mm);
                    double zFim = Math.Min(ma.Z1Mm, mb.Z1Mm);
                    if (zFim - zIni < AlturaMinimaContatoMm) continue;

                    double bu0;
                    double bu1;
                    double bn0;
                    double bn1;
                    CaixaNosEixosDe(ma, mb, out bu0, out bu1, out bn0, out bn1);

                    double folgaU;
                    double folgaN;
                    if (!Encosta(ma, bu0, bu1, bn0, bn1, out folgaU, out folgaN)) continue;

                    double au0;
                    double au1;
                    double an0;
                    double an1;
                    CaixaNosEixosDe(mb, ma, out au0, out au1, out an0, out an1);

                    double folgaU2;
                    double folgaN2;
                    if (!Encosta(mb, au0, au1, an0, an1, out folgaU2, out folgaN2)) continue;

                    // Interface entre os dois montantes, nos eixos de A: o parafuso atravessa o eixo
                    // em que eles mais se encostam (menor sobreposicao).
                    double iniU = Math.Max(ma.U0Mm, bu0);
                    double fimU = Math.Min(ma.U1Mm, bu1);
                    double iniN = Math.Max(ma.N0Mm, bn0);
                    double fimN = Math.Min(ma.N1Mm, bn1);

                    bool atravessaEmU = -folgaU <= -folgaN;
                    double plano = atravessaEmU ? (iniU + fimU) / 2.0 : (iniN + fimN) / 2.0;
                    double centroOutro = atravessaEmU ? (iniN + fimN) / 2.0 : (iniU + fimU) / 2.0;
                    double larguraOutro = atravessaEmU ? -folgaN : -folgaU;
                    double desloca = Math.Min(15.0, Math.Max(0.0, larguraOutro / 2.0 - 10.0));

                    bool aEhDono = a.ParedeId < b.ParedeId
                        || (a.ParedeId == b.ParedeId && a.Painel <= b.Painel);
                    ElementoEstruturaLido dono = aEhDono ? a : b;

                    string categoria = a.ParedeId != b.ParedeId
                        ? CategoriaParedes
                        : (a.Painel != b.Painel ? CategoriaPaineis : CategoriaCostas);

                    List<double> alturas = Distribuir(zIni, zFim, PassoEmendaMm, BordaEmendaMm);
                    for (int k = 0; k < alturas.Count; k++)
                    {
                        double lado = (k % 2 == 0) ? desloca : -desloca;
                        XYZ ponto;
                        XYZ direcao;

                        if (atravessaEmU)
                        {
                            ponto = Ponto(ma, plano - ComprimentoParafusoEstruturaMm / 2.0, centroOutro + lado, alturas[k]);
                            direcao = ma.Dir;
                        }
                        else
                        {
                            ponto = Ponto(ma, centroOutro + lado, plano - ComprimentoParafusoEstruturaMm / 2.0, alturas[k]);
                            direcao = ma.Nor;
                        }

                        resultado.Parafusos.Add(new ParafusoPosicao
                        {
                            Ponto = ponto,
                            Direcao = direcao,
                            ComprimentoMm = ComprimentoParafusoEstruturaMm,
                            Categoria = categoria,
                            ParedeId = dono.ParedeId,
                            Painel = dono.Painel,
                            Descricao = NomeParafusoEstrutura
                        });
                    }

                    resultado.Ligacoes.Add(new LigacaoMontantes
                    {
                        Tipo = categoria,
                        A = a,
                        B = b,
                        Dono = dono,
                        AlturaMm = zFim - zIni,
                        Parafusos = alturas.Count
                    });
                }
            }
        }

        private static void CaixaNosEixosDe(
            MedidaElemento referencia, MedidaElemento outra,
            out double u0, out double u1, out double n0, out double n1)
        {
            u0 = double.MaxValue;
            u1 = double.MinValue;
            n0 = double.MaxValue;
            n1 = double.MinValue;

            foreach (XYZ canto in outra.Cantos)
            {
                double u = LeitorDeEstrutura.Mm(canto.DotProduct(referencia.Dir));
                double n = LeitorDeEstrutura.Mm(canto.DotProduct(referencia.Nor));

                if (u < u0) u0 = u;
                if (u > u1) u1 = u;
                if (n < n0) n0 = n;
                if (n > n1) n1 = n;
            }
        }

        /// <summary>
        /// A caixa do outro encosta (folga ate a tolerancia) ou entra na caixa de referencia. Encostar so
        /// por um canto nao conta.
        /// </summary>
        private static bool Encosta(
            MedidaElemento referencia, double u0, double u1, double n0, double n1,
            out double folgaU, out double folgaN)
        {
            folgaU = Math.Max(referencia.U0Mm, u0) - Math.Min(referencia.U1Mm, u1);
            folgaN = Math.Max(referencia.N0Mm, n0) - Math.Min(referencia.N1Mm, n1);

            if (folgaU > ToleranciaContatoMm || folgaN > ToleranciaContatoMm) return false;

            return -folgaU > SobreposicaoMinimaMm || -folgaN > SobreposicaoMinimaMm;
        }

        // ------------------------------------------------------------------------------------
        // Parafusos das placas
        // ------------------------------------------------------------------------------------

        private static void AdicionarPlacas(
            Document doc, ResultadoParafusos resultado, List<ElementoEstruturaLido> estrutura,
            List<PlacaExistente> placas, List<string> avisos)
        {
            Dictionary<int, List<ElementoEstruturaLido>> porParede = estrutura
                .GroupBy(e => e.ParedeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var eixos = new Dictionary<int, XYZ[]>();
            int semEstrutura = 0;
            int semGeometria = 0;
            int semParafuso = 0;

            foreach (PlacaExistente placa in placas)
            {
                if (!placa.Tipo.HasValue || placa.Elemento == null) continue;

                List<ElementoEstruturaLido> daParede;
                if (!porParede.TryGetValue(placa.ParedeId, out daParede))
                {
                    semEstrutura++;
                    continue;
                }

                XYZ[] eixo;
                if (!eixos.TryGetValue(placa.ParedeId, out eixo))
                {
                    eixo = LeitorDeEstrutura.EixoDaParede(doc, placa.ParedeId);
                    eixos[placa.ParedeId] = eixo;
                }

                MedidaElemento medida = LeitorDeEstrutura.Medir(placa.Elemento, eixo);
                List<Solid> solidos = LeitorDeEstrutura.ColetarSolidos(placa.Elemento);
                if (medida == null || solidos.Count == 0)
                {
                    semGeometria++;
                    continue;
                }

                int antes = resultado.Parafusos.Count;

                try
                {
                    ParafusarPeca(resultado, placa, medida, solidos, daParede);
                }
                catch (Exception ex)
                {
                    avisos.Add("Placa " + placa.Codigo + ": falha ao calcular os parafusos (" + ex.Message + ").");
                }

                if (resultado.Parafusos.Count == antes) semParafuso++;
            }

            if (semEstrutura > 0)
            {
                avisos.Add(semEstrutura + " placa(s) sem a estrutura da parede no projeto: ficaram sem parafusos.");
            }

            if (semGeometria > 0)
            {
                avisos.Add(semGeometria + " placa(s) sem geometria: ficaram sem parafusos.");
            }

            if (semParafuso > 0)
            {
                avisos.Add(semParafuso + " placa(s) sem nenhum parafuso calculado (sem montante ou guia atras). Conferir na aba Parafusos das placas.");
            }
        }

        /// <summary>
        /// Parafusos de uma peca: uma linha por montante que fica atras dela (escolhida onde a peca
        /// tem material, a pelo menos a distancia da borda das bordas), mais uma linha ao longo da
        /// guia nas bordas inferior e superior quando a peca apoia no piso ou no topo.
        /// </summary>
        private static void ParafusarPeca(
            ResultadoParafusos resultado, PlacaExistente placa, MedidaElemento peca,
            List<Solid> solidos, List<ElementoEstruturaLido> daParede)
        {
            RegraFixacaoPlaca regra = PlacaCatalogo.RegraDoTipo(placa.Tipo.Value);
            double espacamento = regra.EspacamentoAoLongoDoApoioMm;
            double borda = regra.DistanciaDaBordaMm;
            double comprimento = ComprimentoDoParafuso(regra.ParafusoPadrao);
            string nomeTipo = PlacaCatalogo.NomeDoTipo(placa.Tipo.Value);

            // A face de fora da placa e a que fica mais longe do centro da estrutura da parede.
            double nPeca = (peca.N0Mm + peca.N1Mm) / 2.0;
            double nEstrutura = daParede.Average(e => (e.Medida.N0Mm + e.Medida.N1Mm) / 2.0);
            bool foraEmN1 = nPeca > nEstrutura;
            double nFace = foraEmN1 ? peca.N1Mm : peca.N0Mm;
            XYZ paraDentro = foraEmN1 ? peca.Nor.Negate() : peca.Nor;

            Action<double, double> adicionar = (uMm, zMm) =>
            {
                resultado.Parafusos.Add(new ParafusoPosicao
                {
                    Ponto = Ponto(peca, uMm, nFace, zMm),
                    Direcao = paraDentro,
                    ComprimentoMm = comprimento,
                    Categoria = CategoriaPlaca,
                    ParedeId = placa.ParedeId,
                    Painel = placa.Painel,
                    Peca = placa.Codigo,
                    Tipo = nomeTipo,
                    Descricao = regra.ParafusoPadrao
                });
            };

            double nMeio = nPeca;
            var linhasU = new List<double>();

            // ---- Montantes atras da peca ----
            List<ElementoEstruturaLido> apoios = daParede
                .Where(e => e.EhMontanteVertical
                    && e.Medida.U1Mm > peca.U0Mm - 5.0 && e.Medida.U0Mm < peca.U1Mm + 5.0
                    && e.Medida.Z1Mm > peca.Z0Mm && e.Medida.Z0Mm < peca.Z1Mm)
                .OrderBy(e => (e.Medida.U0Mm + e.Medida.U1Mm) / 2.0)
                .ToList();

            foreach (ElementoEstruturaLido apoio in apoios)
            {
                double u = MelhorLinhaVertical(solidos, peca, apoio.Medida, borda, nMeio);
                if (double.IsNaN(u)) continue;
                if (linhasU.Any(x => Math.Abs(x - u) < DistanciaMinimaEntreLinhasMm)) continue;

                linhasU.Add(u);

                List<double[]> segmentos = SegmentosVerticais(solidos, peca, u, nMeio, apoio.Medida);
                foreach (double[] segmento in segmentos)
                {
                    foreach (double z in Distribuir(segmento[0], segmento[1], espacamento, borda))
                    {
                        adicionar(u, z);
                    }
                }
            }

            // ---- Guias (piso e topo) ----
            double piso = daParede.Min(e => e.Medida.Z0Mm);
            double topo = daParede.Max(e => e.Medida.Z1Mm);

            var alturasDasGuias = new List<double>();
            if (peca.Z0Mm <= piso + AlcanceDaGuiaMm) alturasDasGuias.Add(peca.Z0Mm + borda);
            if (peca.Z1Mm >= topo - AlcanceDaGuiaMm) alturasDasGuias.Add(peca.Z1Mm - borda);

            foreach (double z in alturasDasGuias)
            {
                XYZ p0 = Ponto(peca, peca.U0Mm - 5.0, nMeio, z);
                XYZ p1 = Ponto(peca, peca.U1Mm + 5.0, nMeio, z);

                foreach (double[] segmento in SegmentosDentro(solidos, p0, p1, peca.Dir))
                {
                    foreach (double u in Distribuir(segmento[0], segmento[1], espacamento, borda))
                    {
                        // O encontro com a linha de um montante ja tem parafuso.
                        if (linhasU.Any(x => Math.Abs(x - u) < DistanciaMinimaDaLinhaVerticalMm)) continue;
                        adicionar(u, z);
                    }
                }
            }
        }

        /// <summary>
        /// Linha vertical de parafusos de um montante: a posicao (ao longo da parede), dentro da mesa
        /// do montante e a pelo menos a distancia da borda das bordas da peca, onde a peca tem mais
        /// material atras; em empate, a mais perto do centro do montante (ou da borda da peca).
        /// NaN se o montante nao apoia a peca.
        /// </summary>
        private static double MelhorLinhaVertical(
            List<Solid> solidos, MedidaElemento peca, MedidaElemento apoio, double borda, double nMeio)
        {
            double ini = Math.Max(apoio.U0Mm + 3.0, peca.U0Mm + borda - 0.5);
            double fim = Math.Min(apoio.U1Mm - 3.0, peca.U1Mm - borda + 0.5);

            if (fim < ini)
            {
                ini = Math.Max(apoio.U0Mm + 3.0, peca.U0Mm + 2.0);
                fim = Math.Min(apoio.U1Mm - 3.0, peca.U1Mm - 2.0);
                if (fim < ini) return double.NaN;
            }

            double alvo = Math.Max(
                peca.U0Mm + borda,
                Math.Min(peca.U1Mm - borda, (apoio.U0Mm + apoio.U1Mm) / 2.0));

            bool achou = false;
            double melhorU = 0.0;
            double melhorComprimento = 0.0;

            for (double u = ini; u <= fim + 0.01; u += PassoBuscaLinhaMm)
            {
                double comprimento = SegmentosVerticais(solidos, peca, u, nMeio, apoio).Sum(s => s[1] - s[0]);
                if (comprimento < 1.0) continue;

                bool maior = comprimento > melhorComprimento + 1.0;
                bool empateMaisPerto = Math.Abs(comprimento - melhorComprimento) <= 1.0
                    && Math.Abs(u - alvo) < Math.Abs(melhorU - alvo);

                if (!achou || maior || empateMaisPerto)
                {
                    achou = true;
                    melhorU = u;
                    melhorComprimento = comprimento;
                }
            }

            if (!achou || melhorComprimento < 2.0 * borda) return double.NaN;
            return melhorU;
        }

        /// <summary>Trechos (z inicial e final, em mm) de uma linha vertical que estao dentro da peca e na altura do montante.</summary>
        private static List<double[]> SegmentosVerticais(
            List<Solid> solidos, MedidaElemento peca, double uMm, double nMeio, MedidaElemento apoio)
        {
            double zIni = Math.Max(peca.Z0Mm, apoio.Z0Mm) - 5.0;
            double zFim = Math.Min(peca.Z1Mm, apoio.Z1Mm) + 5.0;

            var resultado = new List<double[]>();
            if (zFim <= zIni) return resultado;

            XYZ p0 = Ponto(peca, uMm, nMeio, zIni);
            XYZ p1 = Ponto(peca, uMm, nMeio, zFim);

            foreach (double[] s in SegmentosDentro(solidos, p0, p1, XYZ.BasisZ))
            {
                double a = Math.Max(s[0], apoio.Z0Mm);
                double b = Math.Min(s[1], apoio.Z1Mm);
                if (b - a > 1.0) resultado.Add(new[] { a, b });
            }

            return resultado;
        }

        /// <summary>
        /// Trechos de uma linha que estao dentro dos solidos, como pares {inicio, fim} medidos ao
        /// longo do eixoParametro (em mm), em ordem.
        /// </summary>
        private static List<double[]> SegmentosDentro(List<Solid> solidos, XYZ p0, XYZ p1, XYZ eixoParametro)
        {
            var lista = new List<double[]>();
            if (p0.DistanceTo(p1) < 0.01) return lista;

            Line linha;
            try
            {
                linha = Line.CreateBound(p0, p1);
            }
            catch (Exception)
            {
                return lista;
            }

            foreach (Solid solido in solidos)
            {
                try
                {
                    SolidCurveIntersection intersecao = solido.IntersectWithCurve(linha, new SolidCurveIntersectionOptions());
                    if (intersecao == null) continue;

                    for (int i = 0; i < intersecao.SegmentCount; i++)
                    {
                        Curve trecho = intersecao.GetCurveSegment(i);
                        double v0 = LeitorDeEstrutura.Mm(trecho.GetEndPoint(0).DotProduct(eixoParametro));
                        double v1 = LeitorDeEstrutura.Mm(trecho.GetEndPoint(1).DotProduct(eixoParametro));
                        lista.Add(new[] { Math.Min(v0, v1), Math.Max(v0, v1) });
                    }
                }
                catch (Exception)
                {
                    // Sem intersecao calculavel neste solido: segue com os outros.
                }
            }

            return lista.OrderBy(s => s[0]).ToList();
        }

        /// <summary>
        /// Posicoes de parafusos entre ini e fim, a pelo menos 'borda' das pontas e com espacamento
        /// nao maior que o maximo (distribuidos por igual). Trecho curto: um parafuso no meio.
        /// </summary>
        public static List<double> Distribuir(double ini, double fim, double espacamentoMaximo, double borda)
        {
            var posicoes = new List<double>();

            double comprimento = fim - ini;
            if (comprimento <= 0) return posicoes;

            double util = comprimento - 2.0 * borda;
            if (util <= 0)
            {
                posicoes.Add((ini + fim) / 2.0);
                return posicoes;
            }

            int quantidade = (int)Math.Ceiling(util / espacamentoMaximo - 1e-9) + 1;
            if (quantidade < 2) quantidade = 2;

            double passo = util / (quantidade - 1);
            for (int i = 0; i < quantidade; i++)
            {
                posicoes.Add(ini + borda + i * passo);
            }

            return posicoes;
        }

        /// <summary>Le o comprimento (mm) da descricao do parafuso, por exemplo "TA 25 mm" ou "4,2 x 32 mm"; 25 se nao achar.</summary>
        private static double ComprimentoDoParafuso(string descricao)
        {
            Match m = Regex.Match(descricao ?? "", @"(\d+)\s*mm");
            double valor;

            return m.Success && double.TryParse(
                m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
                ? valor
                : 25.0;
        }

        private static XYZ Ponto(MedidaElemento m, double uMm, double nMm, double zMm)
        {
            return m.Dir.Multiply(Interno(uMm)) + m.Nor.Multiply(Interno(nMm)) + XYZ.BasisZ.Multiply(Interno(zMm));
        }

        private static double Interno(double mm)
        {
            return RevitVersionAdapter.MilimetrosParaInterno(mm);
        }

        // ------------------------------------------------------------------------------------
        // 3D
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Cria os parafusos no 3D (um elemento por categoria, parede e painel). Cada parafuso e um
        /// corpo que entra no material mais uma cabeca que fica para fora da face. Antes, apaga os
        /// parafusos 3D criados antes. Devolve quantos criou e quantos falharam.
        /// </summary>
        public static ResultadoCriacao3D Criar3D(Document doc, ResultadoParafusos resultado)
        {
            var saida = new ResultadoCriacao3D();

            using (Transaction transacao = new Transaction(doc, "LSF - Parafusos no 3D"))
            {
                transacao.Start();

                List<ElementId> antigos = new FilteredElementCollector(doc)
                    .OfClass(typeof(DirectShape))
                    .Cast<DirectShape>()
                    .Where(ds =>
                    {
                        string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                        return comentario != null && comentario.StartsWith(PrefixoEtiqueta, StringComparison.Ordinal);
                    })
                    .Select(ds => ds.Id)
                    .ToList();

                if (antigos.Count > 0)
                {
                    doc.Delete(antigos);
                    saida.Apagados = antigos.Count;
                }

                ElementId materialEstrutura = ObterMaterial(doc, "LSF - Parafuso estrutura", new Color(200, 30, 30));
                ElementId materialPlaca = ObterMaterial(doc, "LSF - Parafuso placa", new Color(30, 30, 30));

                foreach (var grupo in resultado.Parafusos
                    .GroupBy(p => new { p.Categoria, p.ParedeId, p.Painel })
                    .OrderBy(g => g.Key.ParedeId).ThenBy(g => g.Key.Painel).ThenBy(g => g.Key.Categoria))
                {
                    ElementId material = grupo.Key.Categoria == CategoriaPlaca ? materialPlaca : materialEstrutura;
                    var solidos = new List<GeometryObject>();
                    int parafusosDoGrupo = 0;

                    foreach (ParafusoPosicao parafuso in grupo)
                    {
                        try
                        {
                            List<Solid> partes = CriarSolidosDoParafuso(parafuso, material);
                            solidos.AddRange(partes);
                            parafusosDoGrupo++;
                        }
                        catch (Exception ex)
                        {
                            saida.Falhas++;
                            if (saida.PrimeiroErro == null) saida.PrimeiroErro = ex.Message;
                        }
                    }

                    if (solidos.Count == 0) continue;

                    DirectShape ds = null;
                    try
                    {
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.SetShape(solidos);
                        ds.Name = "LSF - Parafusos - " + grupo.Key.Categoria + " - P" + grupo.Key.Painel;

                        Parameter comentarioParametro = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                        if (comentarioParametro != null && !comentarioParametro.IsReadOnly)
                        {
                            comentarioParametro.Set(
                                PrefixoEtiqueta + "Parede " + grupo.Key.ParedeId.ToString(CultureInfo.InvariantCulture)
                                + " | Painel " + grupo.Key.Painel.ToString(CultureInfo.InvariantCulture)
                                + " | " + grupo.Key.Categoria
                                + " | " + parafusosDoGrupo.ToString(CultureInfo.InvariantCulture) + " parafusos");
                        }

                        saida.Criados += parafusosDoGrupo;
                        saida.Elementos++;
                    }
                    catch (Exception ex)
                    {
                        saida.Falhas += parafusosDoGrupo;
                        if (saida.PrimeiroErro == null) saida.PrimeiroErro = ex.Message;

                        if (ds != null)
                        {
                            try
                            {
                                doc.Delete(ds.Id);
                            }
                            catch (Exception)
                            {
                                // Elemento vazio que nao foi possivel apagar: segue.
                            }
                        }
                    }
                }

                transacao.Commit();
            }

            return saida;
        }

        /// <summary>
        /// Um parafuso em 3D: o corpo (entra no material, a partir da face) e a cabeca (maior, fica
        /// para fora da face, para o parafuso aparecer mesmo com placas e montantes visiveis).
        /// </summary>
        private static List<Solid> CriarSolidosDoParafuso(ParafusoPosicao parafuso, ElementId materialId)
        {
            XYZ d = parafuso.Direcao.Normalize();
            XYZ a = (Math.Abs(d.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX).CrossProduct(d).Normalize();
            XYZ b = d.CrossProduct(a).Normalize();

            XYZ baseDaCabeca = parafuso.Ponto - d.Multiply(Interno(AlturaDaCabecaMm));

            var partes = new List<Solid>
            {
                CriarPrisma(parafuso.Ponto, d, a, b, RaioDoCorpoMm, parafuso.ComprimentoMm, materialId),
                CriarPrisma(baseDaCabeca, d, a, b, RaioDaCabecaMm, AlturaDaCabecaMm, materialId)
            };

            return partes;
        }

        /// <summary>Prisma hexagonal com a base em 'origem', perpendicular a d, estendido por 'alturaMm' na direcao d.</summary>
        private static Solid CriarPrisma(
            XYZ origem, XYZ d, XYZ a, XYZ b, double raioMm, double alturaMm, ElementId materialId)
        {
            double raio = Interno(raioMm);

            var vertices = new List<XYZ>();
            for (int k = 0; k < 6; k++)
            {
                double angulo = k * Math.PI / 3.0;
                vertices.Add(origem + a.Multiply(raio * Math.Cos(angulo)) + b.Multiply(raio * Math.Sin(angulo)));
            }

            var contorno = new CurveLoop();
            for (int k = 0; k < 6; k++)
            {
                contorno.Append(Line.CreateBound(vertices[k], vertices[(k + 1) % 6]));
            }

            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { contorno },
                d,
                Interno(alturaMm),
                new SolidOptions(materialId, ElementId.InvalidElementId));
        }

        private static ElementId ObterMaterial(Document doc, string nome, Color cor)
        {
            Material existente = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == nome);
            if (existente != null) return existente.Id;

            ElementId id = Material.Create(doc, nome);
            Material novo = doc.GetElement(id) as Material;
            if (novo != null) novo.Color = cor;
            return id;
        }
    }
}