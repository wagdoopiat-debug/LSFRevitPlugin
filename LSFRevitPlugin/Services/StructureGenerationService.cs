using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Geometry;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Services
{
    public class StructureGenerationService
    {
        /// <summary>
        /// Gera a estrutura completa de uma parede. Se a parede ja foi gerada antes, a estrutura
        /// antiga (tudo que foi etiquetado com o Id desta parede, mais os reforcos de vao
        /// reconhecidos pelo nome) e apagada e regenerada com o perfil/espacamento escolhidos.
        /// Tudo dentro de um TransactionGroup: um unico Ctrl+Z desfaz a limpeza e a geracao.
        ///
        /// Se a parede esta painelizada (botao "Painelizar"), a estrutura e gerada por painel:
        /// montantes de extremidade proprios em cada painel, grade de montantes recomecando no
        /// inicio de cada painel e guias cortadas nas divisas. Parede sem paineis = um painel unico.
        ///
        /// vaosPreDetectados: vaos (portas/janelas) ja detectados e, possivelmente, com as
        /// medidas ajustadas pelo usuario (largura, soleira, topo). Se for null, os vaos sao
        /// detectados aqui com as medidas automaticas das esquadrias.
        /// </summary>
        public ResultadoGeracao GerarEstrutura(
            Document doc, Wall parede, double espacamentoCm = 40.0, PerfilSpec perfil = null,
            List<OpeningInfo> vaosPreDetectados = null)
        {
            using (var grupo = new TransactionGroup(doc, "LSF - Gerar/regenerar estrutura da parede"))
            {
                grupo.Start();
                ResultadoGeracao resultado = GerarEstruturaNoGrupo(doc, parede, espacamentoCm, perfil, vaosPreDetectados);
                grupo.Assimilate();
                return resultado;
            }
        }

        private ResultadoGeracao GerarEstruturaNoGrupo(
            Document doc, Wall parede, double espacamentoCm, PerfilSpec perfil,
            List<OpeningInfo> vaosPreDetectados)
        {
            if (perfil == null)
            {
                perfil = PerfilSpec.CriarPadrao();
            }

            LocationCurve curvaParede = parede.Location as LocationCurve;
            Line linhaParede = curvaParede.Curve as Line;
            XYZ direcaoPrincipal = (linhaParede.GetEndPoint(1) - linhaParede.GetEndPoint(0)).Normalize();
            XYZ inicioParede = linhaParede.GetEndPoint(0);
            XYZ fimParede = linhaParede.GetEndPoint(1);
            double comprimentoParedeInterno = linhaParede.Length;

            var openingReinforcement = new OpeningReinforcement();
            List<OpeningInfo> vaos = vaosPreDetectados ?? openingReinforcement.DetectarVaos(doc, parede);

            // Apaga a estrutura anterior desta parede ANTES de planejar: os calculadores abaixo
            // consultam elementos ja existentes (Existe*Em) para nao duplicar reforcos de canto.
            LimparEstruturaAnterior(doc, parede, vaos);

            // Materiais (cores) do sistema escolhido: criados/reaproveitados uma unica vez por
            // geracao, em transacao propria, ANTES de planejar -- a verga e a contra-verga sao
            // construidas dentro de CalcularReforcoVao e ja precisam do material.
            // Drywall = familia azul, Steel Frame = familia laranja.
            MateriaisLsf materiais;
            using (Transaction tMat = new Transaction(doc, "LSF - Preparar materiais"))
            {
                tMat.Start();
                materiais = LsfMateriais.ObterConjunto(doc, perfil.Sistema);
                tMat.Commit();
            }
            ElementId matMontante = materiais.Montante;
            ElementId matGuia = materiais.Guia;
            ElementId matReforcoVao = materiais.ReforcoVao;

            // Paineis da parede (gravados pelo botao "Painelizar"). Sem paineis -- ou com a
            // painelizacao desatualizada porque o comprimento da parede mudou -- vira um painel
            // unico com a parede inteira, e tudo sai como antes.
            bool painelizacaoDesatualizada;
            List<PainelInfo> paineis = PainelizacaoService.ObterPaineis(parede, out painelizacaoDesatualizada);

            // Numero do painel que contem uma posicao (ao longo do eixo da parede).
            int PainelNaPosicao(XYZ ponto)
            {
                double dist = (ponto - inicioParede).DotProduct(direcaoPrincipal);
                foreach (PainelInfo p in paineis)
                {
                    if (dist < p.FimInterno - 1e-6) return p.Numero;
                }
                return paineis[paineis.Count - 1].Numero;
            }

            var wallFraming = new WallFraming();

            // Pontos da grade de montantes, painel por painel. Cada painel tem um montante de
            // extremidade no comeco e outro no fim; EhPontaDaParede marca os que estao nas pontas
            // reais da parede (so esses participam da logica de encontros L/T).
            var pontosGrade = new List<(XYZ Ponto, bool EhExtremidade, bool EhFimDoPainel, bool EhPontaDaParede, int Painel)>();
            foreach (PainelInfo painel in paineis)
            {
                var pontosDoPainel = wallFraming.CalcularPontosMontantesNoIntervalo(
                    parede, espacamentoCm, painel.InicioInterno, painel.FimInterno);

                for (int j = 0; j < pontosDoPainel.Count; j++)
                {
                    XYZ pontoDoPainel = pontosDoPainel[j].Ponto;
                    bool ehExt = pontosDoPainel[j].EhExtremidade;
                    bool ehFim = ehExt && j == pontosDoPainel.Count - 1;
                    bool ehPonta = ehExt
                        && ((!ehFim && painel.Numero == 1)
                            || (ehFim && painel.Numero == paineis[paineis.Count - 1].Numero));

                    pontosGrade.Add((pontoDoPainel, ehExt, ehFim, ehPonta, painel.Numero));
                }
            }

            List<WallJunctionInfo> encontrosL = LJunctionCalculator.DetectarEncontros(doc, parede);
            List<TJunctionInfo> encontrosT = TJunctionCalculator.DetectarEncontrosEmT(doc, parede);
            List<CruzJunctionInfo> encontrosCruz = CruzJunctionCalculator.DetectarEncontrosEmCruz(doc, parede);

            // Pontas das guias nos cantos em L (distancias ao longo do eixo da parede, a partir
            // do inicio dela). Mesma regra do montante isolado do canto: a parede que CRIA o
            // reforco H (a primeira a ser gerada) e a que "chega" -- a guia dela avanca ate o
            // lado de fora da outra parede (meia espessura do nucleo da outra, alem do ponto de
            // encontro). A outra parede recebe o desconto: a guia dela para meia espessura da
            // primeira antes do ponto de encontro.
            double inicioGuiaDist = 0.0;
            double fimGuiaDist = comprimentoParedeInterno;
            foreach (var encontroL in encontrosL)
            {
                bool estaParedeAvanca = !LJunctionCalculator.ExisteReforcoCantoEm(doc, encontroL.PontoEncontro);
                double metadeEspessuraOutra = encontroL.EspessuraParedeConectadaInterna / 2.0;
                double ajuste = estaParedeAvanca ? metadeEspessuraOutra : -metadeEspessuraOutra;

                if (DistanciaPlana(encontroL.PontoEncontro, inicioParede) < 0.05)
                {
                    inicioGuiaDist = -ajuste;
                }
                else if (DistanciaPlana(encontroL.PontoEncontro, fimParede) < 0.05)
                {
                    fimGuiaDist = comprimentoParedeInterno + ajuste;
                }
            }

            var faixasVaos = vaos.Select(v =>
            {
                double centroDist = (v.PontoCentral - inicioParede).DotProduct(direcaoPrincipal);
                return (Inicio: centroDist - v.LarguraInterna / 2.0, Fim: centroDist + v.LarguraInterna / 2.0);
            }).ToList();

            // Posicoes da grade normal que caem dentro de cada vao (para os cripple studs),
            // uma lista por vao, na mesma ordem/indice de 'vaos' e 'faixasVaos'.
            var pontosPorVao = vaos.Select(v => new List<XYZ>()).ToList();

            var pontosH = new List<(XYZ Ponto, XYZ Direcao, string Nome)>();
            var centrosOcupados = new List<XYZ>();
            int hPulados = 0;
            foreach (var encontro in encontrosL)
            {
                var pontosDoH = LJunctionCalculator.CalcularPontosCantoCalifornia(encontro);
                centrosOcupados.AddRange(pontosDoH.Select(p => p.Ponto));

                if (LJunctionCalculator.ExisteReforcoCantoEm(doc, encontro.PontoEncontro))
                {
                    hPulados++;
                    continue;
                }

                string nomeReforco = LJunctionCalculator.GerarNomeReforco(encontro.PontoEncontro);
                foreach (var pontoH in pontosDoH)
                {
                    pontosH.Add((pontoH.Ponto, pontoH.Direcao, nomeReforco));
                }
            }

            var pontosTPassante = new List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm, string Nome)>();
            List<TJunctionInfo> encontrosTComoPassante = TJunctionCalculator.DetectarEncontrosEmTComoPassante(doc, parede);
            foreach (var encontroTP in encontrosTComoPassante)
            {
                string nomeTPassante = TJunctionCalculator.GerarNomeTriplo(encontroTP.PontoT) + "-passante";
                var pontosDoTPassante = TJunctionCalculator.CalcularPontosTPassante(encontroTP);
                centrosOcupados.AddRange(pontosDoTPassante.Select(p => p.Ponto));

                foreach (var pontoTP in pontosDoTPassante)
                {
                    // Os 2 montantes normais (Direcao paralela ao eixo desta parede) ficam de
                    // COSTAS um para o outro, sem se encostar: a alma voltada para o ponto da
                    // conexao e a boca do "C" para fora. A boca aponta para a Direcao, entao cada
                    // um recebe a direcao que se afasta do ponto da conexao ao longo do eixo.
                    // O montante girado 90 graus nao entra aqui (e tratado mais abaixo).
                    XYZ direcaoTP = pontoTP.Direcao;
                    bool paraleloAoEixo = Math.Abs(direcaoTP.Normalize().DotProduct(direcaoPrincipal)) > 0.9;
                    if (paraleloAoEixo)
                    {
                        double ladoDoPontoT = (pontoTP.Ponto - encontroTP.PontoT).DotProduct(direcaoPrincipal);
                        if (Math.Abs(ladoDoPontoT) > 0.003)
                        {
                            direcaoTP = ladoDoPontoT > 0 ? direcaoPrincipal : direcaoPrincipal.Negate();
                        }
                    }

                    pontosTPassante.Add((pontoTP.Ponto, direcaoTP, pontoTP.ProfundidadeMm, nomeTPassante));
                }
            }

            var pontosTriplo = new List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm, string Nome)>();
            int triploPulados = 0;
            foreach (var encontroT in encontrosT)
            {
                var pontosDoTriplo = TJunctionCalculator.CalcularPontosTriplo(encontroT);
                centrosOcupados.AddRange(pontosDoTriplo.Select(p => p.Ponto));

                if (TJunctionCalculator.ExisteTriploEm(doc, encontroT.PontoT))
                {
                    triploPulados++;
                    continue;
                }

                string nomeTriplo = TJunctionCalculator.GerarNomeTriplo(encontroT.PontoT);
                foreach (var pontoT in pontosDoTriplo)
                {
                    pontosTriplo.Add((pontoT.Ponto, pontoT.Direcao, pontoT.ProfundidadeMm, nomeTriplo));
                }
            }

            var pontosQuadruplo = new List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm, string Nome)>();
            int quadruploPulados = 0;
            foreach (var encontroCruz in encontrosCruz)
            {
                bool jaExiste = CruzJunctionCalculator.ExisteQuadruploEm(doc, encontroCruz.PontoCruz);

                if (jaExiste)
                {
                    quadruploPulados++;

                    var pontosCruzada = CruzJunctionCalculator.CalcularPontosQuadruploCruzada(encontroCruz);
                    centrosOcupados.AddRange(pontosCruzada.Select(p => p.Ponto));

                    string nomeCruzada = CruzJunctionCalculator.GerarNomeQuadruplo(encontroCruz.PontoCruz) + "-cruzada";
                    foreach (var pontoC in pontosCruzada)
                    {
                        pontosQuadruplo.Add((pontoC.Ponto, pontoC.Direcao, pontoC.ProfundidadeMm, nomeCruzada));
                    }

                    continue;
                }

                var pontosDoQuadruplo = CruzJunctionCalculator.CalcularPontosQuadruplo(encontroCruz);
                centrosOcupados.AddRange(pontosDoQuadruplo.Select(p => p.Ponto));

                string nomeQuadruplo = CruzJunctionCalculator.GerarNomeQuadruplo(encontroCruz.PontoCruz);
                foreach (var pontoQ in pontosDoQuadruplo)
                {
                    pontosQuadruplo.Add((pontoQ.Ponto, pontoQ.Direcao, pontoQ.ProfundidadeMm, nomeQuadruplo));
                }
            }

            // Orientacao do "C" nos pares encostados dos encontros: a boca do "C" aponta para a
            // Direcao do montante, entao aqui ajustamos a Direcao para que os dois montantes de
            // cada par fiquem DE COSTAS um para o outro (almas voltadas entre si, bocas para fora).
            // Isso nao muda posicao nenhuma (a caixa do montante e simetrica), so o lado da boca.

            // Par H do canto em L: um grupo de 2 montantes por canto (mesmo Nome).
            var hOrientados = new List<(XYZ Ponto, XYZ Direcao, string Nome)>();
            foreach (var grupoH in pontosH.GroupBy(p => p.Nome))
            {
                var listaH = grupoH.ToList();
                XYZ[] direcoesH = DirecoesCostasComCostas(
                    listaH.Select(p => p.Ponto).ToList(),
                    listaH.Select(p => p.Direcao).ToList(),
                    direcaoPrincipal);

                for (int i = 0; i < listaH.Count; i++)
                {
                    hOrientados.Add((listaH[i].Ponto, direcoesH[i], listaH[i].Nome));
                }
            }
            pontosH = hOrientados;

            // Encontro em "+": a primeira parede gera 4 montantes (2 pares costas com costas,
            // espelhados nos dois lados do cruzamento). Os 2 montantes da parede cruzada
            // (nome terminado em "-cruzada") nao se encostam e ficam como estao.
            var quadOrientados = new List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm, string Nome)>();
            foreach (var grupoQ in pontosQuadruplo.GroupBy(p => p.Nome))
            {
                var listaQ = grupoQ.ToList();
                XYZ[] direcoesQ = grupoQ.Key.EndsWith("-cruzada", StringComparison.Ordinal)
                    ? listaQ.Select(p => p.Direcao).ToArray()
                    : DirecoesCostasComCostas(
                        listaQ.Select(p => p.Ponto).ToList(),
                        listaQ.Select(p => p.Direcao).ToList(),
                        direcaoPrincipal);

                for (int i = 0; i < listaQ.Count; i++)
                {
                    quadOrientados.Add((listaQ[i].Ponto, direcoesQ[i], listaQ[i].ProfundidadeMm, listaQ[i].Nome));
                }
            }
            pontosQuadruplo = quadOrientados;

            // T na parede que passa: o montante girado 90 graus (Direcao perpendicular ao eixo
            // desta parede) tem o sentido do "C" invertido -- a alma passa para o lado onde estava
            // a boca e a boca fica para dentro da emenda. Montantes que nao estao girados
            // (Direcao paralela ao eixo) ficam como estao (ja orientados acima, no laco do T).
            var tPassanteOrientados = new List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm, string Nome)>();
            foreach (var tp in pontosTPassante)
            {
                bool girado90 = Math.Abs(tp.Direcao.Normalize().DotProduct(direcaoPrincipal)) < 0.2;
                XYZ direcaoFinal = girado90 ? tp.Direcao.Negate() : tp.Direcao;
                tPassanteOrientados.Add((tp.Ponto, direcaoFinal, tp.ProfundidadeMm, tp.Nome));
            }
            pontosTPassante = tPassanteOrientados;

            double alturaParedeInterna = parede.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble()
                ?? throw new InvalidOperationException("Nao foi possivel ler a altura da parede selecionada.");

            // Vaos: a estrutura antiga ja foi apagada em LimparEstruturaAnterior, entao todo vao
            // e (re)gerado aqui, com as medidas que vieram em 'vaos' (automaticas ou ajustadas).
            // O material de reforco de vao vai junto: verga e contra-verga sao construidas aqui dentro.
            // Todo o reforco de um vao pertence ao painel onde fica o centro do vao.
            var elementosVao = new List<ElementoReforcoVao>();
            var painelPorElementoVao = new List<int>();
            foreach (var vao in vaos)
            {
                var elementosDoVao = openingReinforcement.CalcularReforcoVao(
                    vao, direcaoPrincipal, alturaParedeInterna, perfil, matReforcoVao);
                elementosVao.AddRange(elementosDoVao);

                int painelDoVao = PainelNaPosicao(vao.PontoCentral);
                foreach (var _ in elementosDoVao)
                {
                    painelPorElementoVao.Add(painelDoVao);
                }

                centrosOcupados.AddRange(elementosDoVao.Where(e => e.Ponto != null).Select(e => e.Ponto));
            }

            double distanciaMinimaInterna = RevitVersionAdapter.CentimetrosParaInterno(4);
            // Largura do montante ao longo da parede = aba do perfil escolhido.
            double larguraPerfilInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);
            double folgaEncaixeInterna = RevitVersionAdapter.CentimetrosParaInterno(OpeningReinforcement.FolgaEncaixeGuiaCm);
            double alturaComFolgaDosDoisLados = alturaParedeInterna - (2 * folgaEncaixeInterna);

            // Centros dos montantes de extremidade (das pontas da parede e das divisas de painel),
            // para a regra de colisao com a grade normal.
            foreach (var pg in pontosGrade)
            {
                if (!pg.EhExtremidade) continue;

                bool ehT = pg.EhPontaDaParede && encontrosT.Any(e => e.PontoT.DistanceTo(pg.Ponto) < 0.05);
                if (ehT) continue;

                XYZ direcaoDoMontante = pg.EhFimDoPainel ? direcaoPrincipal.Negate() : direcaoPrincipal;

                double deslocamento = 0;
                if (pg.EhPontaDaParede)
                {
                    WallJunctionInfo encontroLDesseponto = encontrosL.FirstOrDefault(e => e.PontoEncontro.IsAlmostEqualTo(pg.Ponto));
                    if (encontroLDesseponto != null)
                    {
                        bool cantoJaReforcado = LJunctionCalculator.ExisteReforcoCantoEm(doc, encontroLDesseponto.PontoEncontro);
                        deslocamento = cantoJaReforcado
                            ? (encontroLDesseponto.EspessuraParedeConectadaInterna / 2.0)
                            : -(encontroLDesseponto.EspessuraParedeConectadaInterna / 2.0);
                    }
                }

                double halfWInterna = larguraPerfilInterna / 2.0;
                centrosOcupados.Add(pg.Ponto + direcaoDoMontante.Multiply(halfWInterna + deslocamento));
            }

            var resultado = new ResultadoGeracao
            {
                MontantesPrincipais = pontosGrade.Count,
                EncontrosL = encontrosL.Count,
                HJaExistentes = hPulados,
                EncontrosT = encontrosT.Count,
                TriploJaExistentes = triploPulados,
                EncontrosCruz = encontrosCruz.Count,
                QuadruploJaExistentes = quadruploPulados
            };

            var todasPosicoesFinais = new List<double>();

            using (Transaction trans = new Transaction(doc, "LSF - Gerar estrutura completa"))
            {
                trans.Start();

                // Identidade do elemento (parede, painel, tipo e perfil) gravada no Extensible
                // Storage do proprio elemento. E a fonte de verdade da relacao elemento -> painel.
                //
                // No campo Comentarios vai o mesmo texto, para o usuario ler no modelo e para
                // sustentar os elementos gerados antes deste schema existir. Os dois canais sao
                // gravados aqui, juntos, entao nao podem divergir. Se o campo Comentarios estiver
                // somente leitura, o Extensible Storage e gravado assim mesmo.
                void Marcar(DirectShape ds, string tipo, int painel)
                {
                    if (ds == null) return;

                    string resumoPerfil = perfil.DescricaoResumo();

                    PertencenciaPainelService.Gravar(ds, parede.Id.IntegerValue, painel, tipo, resumoPerfil);

                    Parameter comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comentario != null && !comentario.IsReadOnly)
                    {
                        comentario.Set(
                            PrefixoEtiqueta(parede) + "Painel " + painel + " | " + tipo + " | " + resumoPerfil);
                    }
                }

                int criados = 0;
                int pulados = 0;
                int isoladosPuladosPorT = 0;

                foreach (var pg in pontosGrade)
                {
                    XYZ ponto = pg.Ponto;
                    bool ehExtremidade = pg.EhExtremidade;

                    if (ehExtremidade && pg.EhPontaDaParede)
                    {
                        bool ehT = encontrosT.Any(e => e.PontoT.DistanceTo(ponto) < 0.05);
                        if (ehT)
                        {
                            isoladosPuladosPorT++;
                            continue;
                        }
                    }

                    // Divisa de painel que cai dentro de um vao: nao ha montante dentro de porta
                    // ou janela, entao os dois montantes dessa divisa sao pulados.
                    if (ehExtremidade && !pg.EhPontaDaParede)
                    {
                        double distDivisa = (ponto - inicioParede).DotProduct(direcaoPrincipal);
                        if (faixasVaos.Any(f => distDivisa >= f.Inicio && distDivisa <= f.Fim))
                        {
                            pulados++;
                            continue;
                        }
                    }

                    if (!ehExtremidade)
                    {
                        double minhaDist = (ponto - inicioParede).DotProduct(direcaoPrincipal);
                        int indiceVaoAtual = faixasVaos.FindIndex(f => minhaDist >= f.Inicio && minhaDist <= f.Fim);
                        if (indiceVaoAtual >= 0)
                        {
                            pontosPorVao[indiceVaoAtual].Add(ponto);
                            pulados++;
                            continue;
                        }

                        bool colide = centrosOcupados.Any(c => c.DistanceTo(ponto) < distanciaMinimaInterna);
                        if (colide)
                        {
                            pulados++;
                            continue;
                        }
                    }

                    // Montantes de extremidade abrem para dentro do proprio painel: o do comeco
                    // aponta para frente, o do fim aponta para tras. Na divisa, os dois ficam de
                    // costas um para o outro.
                    XYZ direcaoDoMontanteLoop = (ehExtremidade && pg.EhFimDoPainel)
                        ? direcaoPrincipal.Negate()
                        : direcaoPrincipal;

                    double deslocamentoAdicional = 0;
                    if (ehExtremidade && pg.EhPontaDaParede)
                    {
                        WallJunctionInfo encontroLNesseponto = encontrosL.FirstOrDefault(e => e.PontoEncontro.IsAlmostEqualTo(ponto));
                        if (encontroLNesseponto != null)
                        {
                            bool cantoJaReforcado = LJunctionCalculator.ExisteReforcoCantoEm(doc, encontroLNesseponto.PontoEncontro);

                            deslocamentoAdicional = cantoJaReforcado
                                ? (encontroLNesseponto.EspessuraParedeConectadaInterna / 2.0)
                                : -(encontroLNesseponto.EspessuraParedeConectadaInterna / 2.0);
                        }
                    }

                    XYZ pontoComFolga = new XYZ(ponto.X, ponto.Y, ponto.Z + folgaEncaixeInterna);

                    // Montante principal (grade normal e extremidade): secao em "C" do perfil escolhido.
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        pontoComFolga, direcaoDoMontanteLoop, alturaComFolgaDosDoisLados, perfil,
                        ehExtremidade, deslocamentoAdicionalInterno: deslocamentoAdicional,
                        materialId: matMontante);
                    DirectShape dsMontante = StudDirectShapeFactory.CriarElementoMontante(doc, solido);
                    string tipoMontante = (ehExtremidade && !pg.EhPontaDaParede)
                        ? "Montante de divisa de painel"
                        : "Montante principal";
                    Marcar(dsMontante, tipoMontante, pg.Painel);
                    criados++;

                    todasPosicoesFinais.Add((ponto - inicioParede).DotProduct(direcaoPrincipal));
                }

                foreach (var pontoH in pontosH)
                {
                    XYZ pontoHComFolga = new XYZ(pontoH.Ponto.X, pontoH.Ponto.Y, pontoH.Ponto.Z + folgaEncaixeInterna);
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        pontoHComFolga, pontoH.Direcao, alturaComFolgaDosDoisLados, perfil,
                        materialId: matMontante);
                    DirectShape dsH = StudDirectShapeFactory.CriarElementoMontante(doc, solido, pontoH.Nome);
                    Marcar(dsH, "Montante H (canto em L)", PainelNaPosicao(pontoH.Ponto));
                    criados++;
                    todasPosicoesFinais.Add((pontoH.Ponto - inicioParede).DotProduct(direcaoPrincipal));
                }

                // Triplo, T-passante e Quadruplo: a largura (aba), a chapa e o labio vem do perfil
                // escolhido; a profundidade continua vindo dos calculadores de encontro
                // (WallJunctions.cs), que ainda nao recebem o PerfilSpec.
                foreach (var pontoT in pontosTriplo)
                {
                    XYZ pontoTComFolga = new XYZ(pontoT.Ponto.X, pontoT.Ponto.Y, pontoT.Ponto.Z + folgaEncaixeInterna);
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        pontoTComFolga, pontoT.Direcao, alturaComFolgaDosDoisLados,
                        larguraMm: perfil.AbaMm, profundidadeMm: pontoT.ProfundidadeMm,
                        materialId: matMontante,
                        espessuraChapaMm: perfil.EspessuraChapaMm, labioMm: perfil.LabioMm);
                    DirectShape dsT = StudDirectShapeFactory.CriarElementoMontante(doc, solido, pontoT.Nome);
                    Marcar(dsT, "Montante Triplo (encontro em T)", PainelNaPosicao(pontoT.Ponto));
                    criados++;
                    todasPosicoesFinais.Add((pontoT.Ponto - inicioParede).DotProduct(direcaoPrincipal));
                }

                foreach (var pontoTP in pontosTPassante)
                {
                    XYZ pontoTPComFolga = new XYZ(pontoTP.Ponto.X, pontoTP.Ponto.Y, pontoTP.Ponto.Z + folgaEncaixeInterna);
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        pontoTPComFolga, pontoTP.Direcao, alturaComFolgaDosDoisLados,
                        larguraMm: perfil.AbaMm, profundidadeMm: pontoTP.ProfundidadeMm,
                        materialId: matMontante,
                        espessuraChapaMm: perfil.EspessuraChapaMm, labioMm: perfil.LabioMm);
                    DirectShape dsTP = StudDirectShapeFactory.CriarElementoMontante(doc, solido, pontoTP.Nome);
                    Marcar(dsTP, "Montante T (parede passante)", PainelNaPosicao(pontoTP.Ponto));
                    criados++;
                    todasPosicoesFinais.Add((pontoTP.Ponto - inicioParede).DotProduct(direcaoPrincipal));
                }

                foreach (var pontoQ in pontosQuadruplo)
                {
                    XYZ pontoQComFolga = new XYZ(pontoQ.Ponto.X, pontoQ.Ponto.Y, pontoQ.Ponto.Z + folgaEncaixeInterna);
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        pontoQComFolga, pontoQ.Direcao, alturaComFolgaDosDoisLados,
                        larguraMm: perfil.AbaMm, profundidadeMm: pontoQ.ProfundidadeMm,
                        materialId: matMontante,
                        espessuraChapaMm: perfil.EspessuraChapaMm, labioMm: perfil.LabioMm);
                    DirectShape dsQ = StudDirectShapeFactory.CriarElementoMontante(doc, solido, pontoQ.Nome);
                    Marcar(dsQ, "Montante Quadruplo (encontro em +)", PainelNaPosicao(pontoQ.Ponto));
                    criados++;
                    todasPosicoesFinais.Add((pontoQ.Ponto - inicioParede).DotProduct(direcaoPrincipal));
                }

                // King/Jack sao construidos aqui (com cor de reforco de vao e secao em "C", ja
                // com a alma voltada para o vao). Verga e contra-verga chegam com
                // SolidoPreConstruido, feito dentro de OpeningReinforcement.cs, ja com a mesma
                // cor e a secao em "U".
                for (int iv = 0; iv < elementosVao.Count; iv++)
                {
                    ElementoReforcoVao elVao = elementosVao[iv];
                    Solid solido = elVao.SolidoPreConstruido ?? StudSolidBuilder.CriarSolidoMontante(
                        elVao.Ponto, elVao.Direcao, elVao.AlturaInterna,
                        larguraMm: elVao.LarguraMm, profundidadeMm: elVao.ProfundidadeMm,
                        materialId: matReforcoVao,
                        espessuraChapaMm: perfil.EspessuraChapaMm, labioMm: perfil.LabioMm);
                    DirectShape dsVao = StudDirectShapeFactory.CriarElementoMontante(doc, solido, elVao.Nome);
                    Marcar(dsVao, "Reforco de vao (King/Jack/verga/contra-verga)", painelPorElementoVao[iv]);
                    criados++;
                }

                // Cripple studs: gerados para todos os vaos, usando as posicoes da grade normal
                // que caem dentro de cada vao.
                var elementosCripple = new List<ElementoReforcoVao>();
                for (int iv = 0; iv < vaos.Count; iv++)
                {
                    if (pontosPorVao[iv].Count == 0) continue;

                    var cripplesDoVao = openingReinforcement.CalcularCrippleStuds(
                        vaos[iv], pontosPorVao[iv], direcaoPrincipal, alturaParedeInterna, perfil);
                    elementosCripple.AddRange(cripplesDoVao);
                }

                foreach (var elCripple in elementosCripple)
                {
                    Solid solido = StudSolidBuilder.CriarSolidoMontante(
                        elCripple.Ponto, elCripple.Direcao, elCripple.AlturaInterna,
                        larguraMm: elCripple.LarguraMm, profundidadeMm: elCripple.ProfundidadeMm,
                        materialId: matReforcoVao,
                        espessuraChapaMm: perfil.EspessuraChapaMm, labioMm: perfil.LabioMm);
                    DirectShape dsCripple = StudDirectShapeFactory.CriarElementoMontante(doc, solido, elCripple.Nome);
                    Marcar(dsCripple, "Cripple stud", PainelNaPosicao(elCripple.Ponto));
                    criados++;
                }

                // Guias (trilhos horizontais inferior e superior), em "U", painel por painel. A
                // guia de cada painel vai de uma divisa a outra; nas pontas reais da parede ela usa
                // inicioGuiaDist / fimGuiaDist (que ja consideram o avanco/desconto nos cantos em
                // L). A guia inferior de cada painel e interrompida nos vaos de porta.
                double alturaGuiaInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);
                int totalTrechosInferiores = 0;

                foreach (PainelInfo painel in paineis)
                {
                    double iniPainel = painel.Numero == 1 ? inicioGuiaDist : painel.InicioInterno;
                    double fimPainel = painel.Numero == paineis[paineis.Count - 1].Numero ? fimGuiaDist : painel.FimInterno;

                    // Guia inferior: uma porta nao tem guia embaixo, entao a guia e dividida em
                    // trechos que pulam a largura (ja ajustada, se for o caso) de cada vao de porta.
                    List<(double Inicio, double Fim)> trechosGuiaInferior = CalcularTrechosGuiaInferior(
                        vaos, inicioParede, direcaoPrincipal, iniPainel, fimPainel);

                    foreach (var trecho in trechosGuiaInferior)
                    {
                        XYZ inicioTrecho = inicioParede + direcaoPrincipal.Multiply(trecho.Inicio);
                        Solid solidoTrechoInferior = GuiaBuilder.CriarSolidoGuia(
                            inicioTrecho, direcaoPrincipal, trecho.Fim - trecho.Inicio, perfil, zBase: 0,
                            materialId: matGuia, abertoParaCima: true);
                        DirectShape dsGuiaInf = StudDirectShapeFactory.CriarElementoMontante(doc, solidoTrechoInferior, "LSF - Guia Inferior");
                        Marcar(dsGuiaInf, "Guia inferior", painel.Numero);
                        criados++;
                    }
                    totalTrechosInferiores += trechosGuiaInferior.Count;

                    // Guia superior: alma em cima, abas para baixo (os montantes entram por baixo).
                    XYZ inicioGuiaSuperior = inicioParede + direcaoPrincipal.Multiply(iniPainel);
                    Solid solidoGuiaSuperior = GuiaBuilder.CriarSolidoGuia(
                        inicioGuiaSuperior, direcaoPrincipal, fimPainel - iniPainel, perfil,
                        zBase: alturaParedeInterna - alturaGuiaInterna,
                        materialId: matGuia, abertoParaCima: false);
                    DirectShape dsGuiaSup = StudDirectShapeFactory.CriarElementoMontante(doc, solidoGuiaSuperior, "LSF - Guia Superior");
                    Marcar(dsGuiaSup, "Guia superior", painel.Numero);
                    criados++;
                }

                trans.Commit();

                resultado.MontantesH = pontosH.Count;
                resultado.MontantesTriplo = pontosTriplo.Count + pontosTPassante.Count;
                resultado.MontantesQuadruplo = pontosQuadruplo.Count;
                resultado.Reposicionados = pulados;
                resultado.IsoladosPuladosPorT = isoladosPuladosPorT;
                resultado.TotalCriados = criados;
                resultado.VaosDetectados = vaos.Count;
                resultado.VaosJaReforcados = 0; // vaos sao sempre regenerados, nunca pulados
                resultado.ElementosDeVao = elementosVao.Count + elementosCripple.Count;
                resultado.GuiasCriadas = totalTrechosInferiores + paineis.Count; // trechos da inferior + 1 superior por painel

                todasPosicoesFinais.Sort();
                double maiorVao = 0;
                for (int i = 1; i < todasPosicoesFinais.Count; i++)
                {
                    double vaoCm = RevitVersionAdapter.InternoParaCentimetros(
                        todasPosicoesFinais[i] - todasPosicoesFinais[i - 1]);
                    if (vaoCm > maiorVao) maiorVao = vaoCm;
                }
                resultado.MaiorVaoCm = maiorVao;
                resultado.VaoExcedeuLimite = maiorVao > espacamentoCm + 0.5;
            }

            return resultado;
        }

        /// <summary>
        /// Distancia entre dois pontos ignorando a cota Z (so X/Y), em unidades internas.
        /// </summary>
        private static double DistanciaPlana(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Para um grupo de montantes paralelos ao eixo da parede, devolve a Direcao (= para onde
        /// a boca do "C" aponta) de cada um, de modo que, a cada 2 montantes consecutivos
        /// (ordenados ao longo do eixo), os dois fiquem DE COSTAS um para o outro: o de tras
        /// com a boca para tras, o da frente com a boca para a frente, almas voltadas entre si.
        /// Se o grupo tiver quantidade impar, algum montante nao for paralelo ao eixo ou os dois
        /// de um par estiverem na mesma posicao, as direcoes originais sao mantidas.
        /// </summary>
        private static XYZ[] DirecoesCostasComCostas(
            IList<XYZ> centros, IList<XYZ> direcoesAtuais, XYZ eixo)
        {
            XYZ[] resultado = direcoesAtuais.ToArray();
            int n = centros.Count;
            if (n < 2 || n % 2 != 0) return resultado;

            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(direcoesAtuais[i].Normalize().DotProduct(eixo)) < 0.99) return resultado;
            }

            List<int> ordem = Enumerable.Range(0, n)
                .OrderBy(i => centros[i].DotProduct(eixo))
                .ToList();

            for (int k = 0; k + 1 < n; k += 2)
            {
                int atras = ordem[k];
                int frente = ordem[k + 1];

                // Menos de ~1 mm de diferenca: nao da para saber quem e quem, mantem como esta.
                if (centros[frente].DotProduct(eixo) - centros[atras].DotProduct(eixo) < 0.003) continue;

                resultado[atras] = eixo.Negate();
                resultado[frente] = eixo;
            }

            return resultado;
        }

        /// <summary>
        /// Divide a guia inferior de um trecho da parede em trechos menores, retirando a largura
        /// de cada vao de porta. Os trechos sao (inicio, fim) em distancia ao longo do eixo da
        /// parede, em unidades internas, medidos a partir do inicio da parede, dentro do intervalo
        /// [inicioGuiaDist, fimGuiaDist] (que pode passar das pontas da parede nos cantos em L).
        /// Janelas nao interrompem a guia. Portas que se sobrepoem ou passam das pontas sao
        /// tratadas sem gerar trechos de comprimento ~zero.
        /// </summary>
        private static List<(double Inicio, double Fim)> CalcularTrechosGuiaInferior(
            List<OpeningInfo> vaos, XYZ inicioParede, XYZ direcaoPrincipal,
            double inicioGuiaDist, double fimGuiaDist)
        {
            // Trechos menores que ~1 mm (em pes) sao descartados.
            const double toleranciaInterna = 0.003;

            var cortes = vaos
                .Where(v => v.Categoria == "Porta")
                .Select(v =>
                {
                    double centro = (v.PontoCentral - inicioParede).DotProduct(direcaoPrincipal);
                    return (Inicio: Math.Max(inicioGuiaDist, centro - v.LarguraInterna / 2.0),
                            Fim: Math.Min(fimGuiaDist, centro + v.LarguraInterna / 2.0));
                })
                .Where(c => c.Fim > c.Inicio)
                .OrderBy(c => c.Inicio)
                .ToList();

            var trechos = new List<(double Inicio, double Fim)>();
            double cursor = inicioGuiaDist;

            foreach (var corte in cortes)
            {
                if (corte.Inicio - cursor > toleranciaInterna)
                {
                    trechos.Add((cursor, corte.Inicio));
                }
                cursor = Math.Max(cursor, corte.Fim);
            }

            if (fimGuiaDist - cursor > toleranciaInterna)
            {
                trechos.Add((cursor, fimGuiaDist));
            }

            return trechos;
        }

        /// <summary>
        /// Texto que abre o campo Comentarios de todo elemento gerado para uma parede. E ele que
        /// identifica, numa nova geracao, o que pertence a esta parede. O " | " apos o Id evita
        /// confundir a parede 1234 com a 12345.
        /// </summary>
        private static string PrefixoEtiqueta(Wall parede)
        {
            return "LSF | Parede " + parede.Id + " | ";
        }

        /// <summary>
        /// Apaga (em transacao propria) tudo que uma geracao anterior criou para esta parede:
        /// os elementos etiquetados com o Id da parede e, para geracoes feitas antes da
        /// etiquetagem existir, os reforcos de vao reconhecidos pelo nome.
        /// </summary>
        private static void LimparEstruturaAnterior(Document doc, Wall parede, List<OpeningInfo> vaos)
        {
            string prefixo = PrefixoEtiqueta(parede);
            List<string> nomesVao = vaos.Select(v => OpeningReinforcement.GerarNomeVao(v.ElementoId)).ToList();

            var idsParaExcluir = new HashSet<ElementId>();
            foreach (DirectShape ds in new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).Cast<DirectShape>())
            {
                string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                if (comentario != null && comentario.StartsWith(prefixo, StringComparison.Ordinal))
                {
                    idsParaExcluir.Add(ds.Id);
                    continue;
                }

                if (nomesVao.Any(n => PertenceAoVao(ds.Name, n)))
                {
                    idsParaExcluir.Add(ds.Id);
                }
            }

            if (idsParaExcluir.Count == 0) return;

            using (Transaction t = new Transaction(doc, "LSF - Limpar estrutura anterior"))
            {
                t.Start();
                doc.Delete(idsParaExcluir.ToList());
                t.Commit();
            }
        }

        /// <summary>
        /// Diz se um DirectShape e elemento de um vao: o nome comeca pelo nome do vao e o
        /// caractere seguinte nao e digito. A segunda condicao evita que o vao de ElementId 1234
        /// "capture" elementos do vao 12345, cujo nome tem o mesmo prefixo.
        /// </summary>
        private static bool PertenceAoVao(string nomeElemento, string nomeVao)
        {
            if (string.IsNullOrEmpty(nomeElemento) || string.IsNullOrEmpty(nomeVao)) return false;
            if (!nomeElemento.StartsWith(nomeVao, StringComparison.Ordinal)) return false;
            if (nomeElemento.Length == nomeVao.Length) return true;
            return !char.IsDigit(nomeElemento[nomeVao.Length]);
        }
    }
}