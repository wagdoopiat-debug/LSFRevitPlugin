using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.Geometry
{
    public sealed class PosicaoBlocking
    {
        /// <summary>
        /// Meio do vao entre os dois montantes adjacentes, na cota absoluta da fileira. Usado
        /// para identificar o painel e para diagnostico; a criacao do solido usa PontoInicial.
        /// </summary>
        public XYZ Centro { get; set; }

        /// <summary>
        /// Extremidade de tras do blank da guia (montante a), na cota absoluta da fileira: fica
        /// uma profundidade de perna ANTES da face interna do montante. A linha de dobra coincide
        /// com a face interna do montante e a perna dobrada abraca o montante; o corpo horizontal
        /// comeca exatamente nessa face. O solido do blocking comeca aqui.
        /// </summary>
        public XYZ PontoInicial { get; set; }

        /// <summary>
        /// Extremidade da frente do blank da guia (montante b), na cota absoluta da fileira: fica
        /// uma profundidade de perna DEPOIS da face interna do montante. A linha de dobra coincide
        /// com a face interna do montante; o corpo horizontal termina exatamente nessa face.
        /// </summary>
        public XYZ PontoFinal { get; set; }

        public XYZ DirecaoSpanLong { get; set; }

        /// <summary>
        /// Comprimento do blank ao longo do eixo (= distancia entre PontoInicial e PontoFinal):
        /// o vao entre as faces internas dos montantes MAIS as duas profundidades de perna, ja que
        /// cada extremidade e dobrada para baixo abracando o montante.
        /// </summary>
        public double ComprimentoAoLongoEixo { get; set; }
        public int Painel { get; set; }
        public double AlturaZ { get; set; }

        /// <summary>Altura da fileira, relativa a base da parede, em cm (dado de configuracao).</summary>
        public double AlturaRelativaCm { get; set; }

        /// <summary>Finalidade registrada para este bloco (travamento, apoio das placas ou ambas).</summary>
        public FinalidadeBlocking Finalidade { get; set; } = FinalidadeBlocking.Ambas;
    }

    public static class BlockingCalculator
    {
        public static List<PosicaoBlocking> Calcular(
            Wall parede,
            XYZ direcaoPrincipal,
            XYZ inicioParede,
            List<(XYZ Ponto, bool EhExtremidade, bool EhFimDoPainel, bool EhPontaDaParede, int Painel)> pontosGrade,
            List<OpeningInfo> vaos,
            BlockingConfig config,
            PerfilSpec perfil,
            Func<XYZ, int> painelNaPosicao,
            double? alturaUtilInterna = null,
            List<string> avisos = null)
        {
            var resultado = new List<PosicaoBlocking>();
            if (direcaoPrincipal == null || inicioParede == null || pontosGrade == null || vaos == null || config == null || perfil == null)
                return resultado;
            config.Validar();
            if (!config.Habilitado) return resultado;

            XYZ dirEixo = direcaoPrincipal.Normalize();
            XYZ nor = new XYZ(-dirEixo.Y, dirEixo.X, 0.0).Normalize();

            var porPainel = pontosGrade
                .GroupBy(p => p.Painel)
                .Select(g => new
                {
                    Painel = g.Key,
                    Pontos = g.OrderBy(p => (p.Ponto - inicioParede).DotProduct(dirEixo)).ToList()
                })
                .ToList();

            double alturaParedeUtil = alturaUtilInterna ?? 0.0;
            if (!alturaUtilInterna.HasValue && parede != null)
            {
                var ph = parede.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                if (ph != null) alturaParedeUtil = ph.AsDouble();
            }

            double folgaGuias = RevitVersionAdapter.CentimetrosParaInterno(OpeningReinforcement.FolgaEncaixeGuiaCm);
            double zBase = folgaGuias;
            double zTopo = alturaParedeUtil - folgaGuias;
            if (zTopo <= zBase + 1e-6) return resultado;

            double larguraAbaInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);
            double meiaAbaInterna = larguraAbaInterna * 0.5;
            double minCompInterna = RevitVersionAdapter.MilimetrosParaInterno(120.0);
            double tolVaoZ = RevitVersionAdapter.MilimetrosParaInterno(5.0);
            // Profundidade da perna dobrada: o blank se estende para tras/para frente da face
            // interna do montante nessa profundidade, e e ali que a secao inteira e dobrada 90
            // graus para baixo abracando o montante (mesma constante usada pelo builder).
            double profundidadePerna = RevitVersionAdapter.MilimetrosParaInterno(
                BlockingTrackBuilder.ProfundidadePernaBlockingMm);
            bool distribuirAlternado = config.Distribuicao == DistribuicaoBlocking.Alternado;
            int faseAlternada = config.AlternarInvertido ? 1 : 0;

            // Fileiras efetivas: alturas explicitas do usuario > juntas das placas (se houver) >
            // distribuicao automatica de Fileiras. Nenhuma altura e inventada sem dados.
            double alturaParedeUtilCm = RevitVersionAdapter.InternoParaCentimetros(alturaParedeUtil);
            double folgaGuiasCm = RevitVersionAdapter.InternoParaCentimetros(folgaGuias);
            List<string> avisosCalc = avisos ?? new List<string>();
            List<double> alturasRelativasCm = BlockingRowsResolver.ResolverAlturasRelativasCm(
                config, alturaParedeUtilCm, folgaGuiasCm, avisosCalc);
            if (alturasRelativasCm.Count == 0) return resultado;

            foreach (var gp in porPainel)
            {
                var pts = gp.Pontos;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    // Distribuicao alternada: mantem apenas os vaos cujo montante inicial tem a
                    // paridade escolhida. 'i' e a posicao do montante na lista ja ordenada por
                    // posicao espacial estavel ao longo do eixo, entao o padrao e deterministico.
                    if (distribuirAlternado && (i % 2) != faseAlternada) continue;

                    var a = pts[i];
                    var b = pts[i + 1];
                    if (a.Painel != b.Painel) continue;

                    double da = (a.Ponto - inicioParede).DotProduct(dirEixo);
                    double db = (b.Ponto - inicioParede).DotProduct(dirEixo);
                    double distCentro = db - da;
                    if (distCentro <= 0.0) continue;

                    // Faces internas dos montantes adjacentes, usando a posicao REAL de montagem:
                    // - extremidade de COMECO de painel (EhExtremidade e !EhFimDoPainel): o
                    //   montante tem a face externa colada na divisa e ocupa +aba; a face
                    //   interna voltada para o vao fica em +aba.
                    // - montante regular: centrado na grade, cada face interna a +-aba/2.
                    // - extremidade de FIM de painel (EhExtremidade e EhFimDoPainel): ocupa
                    //   -aba; a face interna voltada para o vao fica em -aba.
                    double offA = (a.EhExtremidade && !a.EhFimDoPainel) ? larguraAbaInterna : meiaAbaInterna;
                    double offB = (b.EhExtremidade && b.EhFimDoPainel) ? larguraAbaInterna : meiaAbaInterna;
                    XYZ deslocA = dirEixo.Multiply(offA);
                    XYZ deslocB = dirEixo.Multiply(offB);

                    XYZ faceA = a.Ponto + deslocA;
                    XYZ faceB = b.Ponto - deslocB;

                    double compEntreFaces = distCentro - deslocA.GetLength() - deslocB.GetLength();
                    if (compEntreFaces < minCompInterna) continue;

                    // O blank da guia se estende uma profundidade de perna para fora de cada face
                    // interna: a linha de dobra cai exatamente na face do montante e a perna
                    // dobrada abraca o montante (corpo horizontal integro de face a face, sem
                    // lacuna).
                    XYZ pontoInicial = faceA - dirEixo.Multiply(profundidadePerna);
                    XYZ pontoFinal = faceB + dirEixo.Multiply(profundidadePerna);
                    double comprimentoEixo = compEntreFaces + 2.0 * profundidadePerna;

                    XYZ centroEntre = new XYZ(
                        (faceA.X + faceB.X) * 0.5,
                        (faceA.Y + faceB.Y) * 0.5,
                        faceA.Z);
                    int painelBloco = painelNaPosicao != null ? painelNaPosicao(centroEntre) : gp.Painel;

                    // Cota absoluta: tudo dentro da parede (montantes, guias, verga, cripples)
                    // soma a elevacao da base da parede (ponto na curva de localizacao) com a
                    // altura relativa. Repete aqui para o blocking nao nascer "no chao" quando a
                    // base da parede nao esta no nivel 0.
                    double cotaBase = a.Ponto.Z;

                    for (int r = 0; r < alturasRelativasCm.Count; r++)
                    {
                        double hCm = alturasRelativasCm[r];
                        double zf = RevitVersionAdapter.CentimetrosParaInterno(hCm);
                        double zAbs = cotaBase + zf;

                        bool conflitaVao = false;
                        foreach (var v in vaos)
                        {
                            double meio = (v.PontoCentral - inicioParede).DotProduct(dirEixo);
                            double meioV0 = meio - v.LarguraInterna * 0.5;
                            double meioV1 = meio + v.LarguraInterna * 0.5;
                            if (db > meioV0 - 1e-6 && da < meioV1 + 1e-6)
                            {
                                double soleira = v.AlturaSoleiraInterna;
                                double topoVao = soleira + v.AlturaInterna;
                                if (zf < topoVao + tolVaoZ && zf > soleira - tolVaoZ)
                                {
                                    conflitaVao = true;
                                    break;
                                }
                            }
                        }
                        if (conflitaVao) continue;

                        resultado.Add(new PosicaoBlocking
                        {
                            Centro = new XYZ(centroEntre.X, centroEntre.Y, zAbs),
                            PontoInicial = new XYZ(pontoInicial.X, pontoInicial.Y, zAbs),
                            PontoFinal = new XYZ(pontoFinal.X, pontoFinal.Y, zAbs),
                            DirecaoSpanLong = dirEixo,
                            ComprimentoAoLongoEixo = comprimentoEixo,
                            Painel = painelBloco,
                            AlturaZ = zAbs,
                            AlturaRelativaCm = hCm,
                            Finalidade = config.Finalidade
                        });
                    }
                }
            }
            return resultado;
        }
    }
}