using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Layout das vigas da laje: posicoes transversais (v), segmentos ao longo de u e guias de
    /// borda, a partir do contorno e da orientacao (OrientacaoVigas, calculada do mesmo contorno).
    /// Regras:
    ///  - primeira viga na borda inicial (v minimo); demais em k x espacamento; viga na borda final
    ///    se sobrar mais de 1 mm (o ultimo vao fica menor ou igual ao espacamento);
    ///  - as vigas de borda (primeira e ultima) ficam FIXAS nas extremidades do contorno e nunca
    ///    sao alinhadas a apoios;
    ///  - cada viga interna a ate toleranciaAlinhamentoMm de um apoio (posicao v de montante) e
    ///    deslocada para ele;
    ///  - vigas a ate 1 mm uma da outra apos o alinhamento colidem; regra deterministica de quem
    ///    fica: (1) viga de borda; (2) viga alinhada sobre nao alinhada; (3) entre alinhadas, a de
    ///    menor deslocamento e, no empate, a de menor posicao de grade. Cada remocao gera aviso;
    ///  - vao final maior que o espacamento (+1 mm) gera aviso;
    ///  - guia de borda em cada aresta perpendicular as vigas (onde elas terminam);
    ///  - v1: so contorno ortogonal (arestas paralelas ou perpendiculares as vigas, tolerancia 1 mm).
    /// As vigas de borda ficam com o EIXO sobre o contorno; o recuo da meia largura e da geometria.
    /// Com largura fisica informada, os segmentos sao limitados a pegada do perfil no contorno.
    /// </summary>
    public static class LayoutVigas
    {
        /// <summary>Tolerancia geral (mm): coincidencia de posicoes, segmentos curtos, paralelismo.</summary>
        public const double ToleranciaMm = 1.0;

        // Deslocamento (mm) da linha de varredura para fora da propria posicao da viga, para
        // nao cair exatamente sobre vertices ou arestas do contorno.
        private const double DeslocamentoVarreduraMm = 0.01;

        /// <summary>
        /// Posicao transversal efetiva do eixo fisico da viga. Com largura positiva, recua a viga
        /// para dentro dos limites do contorno quando a pegada do perfil ultrapassaria vMin/vMax.
        /// Sem largura, devolve a posicao original.
        /// </summary>
        public static double PosicaoFisicaTransversalMm(
            OrientacaoVigas orientacao, double posicaoEixoMm, double larguraVigaMm)
        {
            if (orientacao == null) throw new ArgumentNullException(nameof(orientacao));
            if (double.IsNaN(larguraVigaMm) || double.IsInfinity(larguraVigaMm) || larguraVigaMm <= 0.0)
                return posicaoEixoMm;

            double meiaLargura = larguraVigaMm / 2.0;
            double minimo = orientacao.VMinMm + meiaLargura;
            double maximo = orientacao.VMaxMm - meiaLargura;

            if (minimo > maximo)
                return (orientacao.VMinMm + orientacao.VMaxMm) / 2.0;
            if (posicaoEixoMm < minimo) return minimo;
            if (posicaoEixoMm > maximo) return maximo;
            return posicaoEixoMm;
        }

        private struct PosicaoViga
        {
            public double Posicao;
            public double Grade;
            public bool Alinhada;
            public bool Borda;
        }

        /// <param name="posicoesApoioMm">Posicoes v (mm) dos apoios (montantes); nulo = sem alinhamento.</param>
        /// <param name="toleranciaAlinhamentoMm">Distancia maxima (mm) para alinhar uma viga interna a um apoio;
        /// 0 = nao alinha; negativo, NaN ou infinito = erro.</param>
        /// <param name="larguraVigaMm">Largura fisica transversal da viga (mm). Se maior que zero, os
        /// segmentos sao limitados a pegada fisica do perfil e avisa quando duas vigas ficam mais
        /// proximas que isso. 0 = segmentos pelo eixo e sem este aviso; negativo, NaN ou infinito = erro.</param>
        public static ResultadoLayoutVigas Calcular(
            ContornoLaje contorno,
            OrientacaoVigas orientacao,
            double espacamentoMm,
            IEnumerable<double> posicoesApoioMm = null,
            double toleranciaAlinhamentoMm = 0.0,
            double larguraVigaMm = 0.0)
        {
            if (contorno == null) throw new ArgumentNullException(nameof(contorno));
            if (orientacao == null) throw new ArgumentNullException(nameof(orientacao));

            var resultado = new ResultadoLayoutVigas();

            resultado.Erros.AddRange(contorno.Validar());

            if (double.IsNaN(espacamentoMm) || double.IsInfinity(espacamentoMm) || espacamentoMm <= 0.0)
            {
                resultado.Erros.Add("Espacamento deve ser maior que zero.");
            }

            if (double.IsNaN(toleranciaAlinhamentoMm) || double.IsInfinity(toleranciaAlinhamentoMm)
                || toleranciaAlinhamentoMm < 0.0)
            {
                resultado.Erros.Add("Tolerancia de alinhamento deve ser um numero finito maior ou igual a zero.");
            }

            if (double.IsNaN(larguraVigaMm) || double.IsInfinity(larguraVigaMm) || larguraVigaMm < 0.0)
            {
                resultado.Erros.Add("Largura da viga deve ser um numero finito maior ou igual a zero.");
            }

            if (resultado.Erros.Count > 0) return resultado;

            IReadOnlyList<Ponto2DMm> pontos = contorno.Pontos;
            int n = pontos.Count;
            var us = new double[n];
            var vs = new double[n];
            for (int i = 0; i < n; i++)
            {
                us[i] = orientacao.CoordenadaU(pontos[i]);
                vs[i] = orientacao.CoordenadaV(pontos[i]);
            }

            double uMin = us[0];
            double uMax = us[0];
            double vMin = vs[0];
            double vMax = vs[0];
            for (int i = 1; i < n; i++)
            {
                if (us[i] < uMin) uMin = us[i];
                if (us[i] > uMax) uMax = us[i];
                if (vs[i] < vMin) vMin = vs[i];
                if (vs[i] > vMax) vMax = vs[i];
            }

            // A orientacao deve ter sido calculada a partir deste contorno: os envolventes coincidem.
            if (Math.Abs(uMin - orientacao.UMinMm) > ToleranciaMm
                || Math.Abs(uMax - orientacao.UMaxMm) > ToleranciaMm
                || Math.Abs(vMin - orientacao.VMinMm) > ToleranciaMm
                || Math.Abs(vMax - orientacao.VMaxMm) > ToleranciaMm)
            {
                resultado.Erros.Add(
                    "Orientacao das vigas nao corresponde ao contorno informado (retangulo envolvente diferente).");
                return resultado;
            }

            if (!ArestasOrtogonais(us, vs))
            {
                resultado.Erros.Add(
                    "Contorno nao ortogonal em relacao a direcao das vigas: na v1 todas as arestas devem ser " +
                    "paralelas ou perpendiculares as vigas.");
                return resultado;
            }

            // 1. Grade de posicoes (primeira e ultima sao bordas) e alinhamento aos apoios.
            List<double> grade = PosicoesDaGrade(vMin, vMax, espacamentoMm);
            var itens = new List<PosicaoViga>();
            for (int i = 0; i < grade.Count; i++)
            {
                itens.Add(new PosicaoViga
                {
                    Posicao = grade[i],
                    Grade = grade[i],
                    Alinhada = false,
                    Borda = i == 0 || i == grade.Count - 1
                });
            }

            if (posicoesApoioMm != null && toleranciaAlinhamentoMm > 0.0)
            {
                int naoFinitos = AlinharAosApoios(itens, posicoesApoioMm, toleranciaAlinhamentoMm, vMin, vMax);
                if (naoFinitos > 0)
                {
                    resultado.Avisos.Add(Formatar(
                        "Posicoes de apoio nao finitas foram ignoradas (quantidade: {0}).", naoFinitos));
                }
            }

            itens.Sort(CompararPosicao);
            List<PosicaoViga> unicos = RemoverColisoes(itens, resultado.Avisos);

            // Aviso (informativo) quando o alinhamento foi pedido mas alguma viga interna nao
            // encontrou montante/apoio correspondente dentro da tolerancia: a viga permanece na
            // grade e isso e reportado em vez de deslocar silenciosamente.
            if (posicoesApoioMm != null && toleranciaAlinhamentoMm > 0.0)
            {
                int internas = 0;
                int internasNaoAlinhadas = 0;
                foreach (PosicaoViga item in unicos)
                {
                    if (item.Borda) continue;
                    internas++;
                    if (!item.Alinhada) internasNaoAlinhadas++;
                }

                if (internasNaoAlinhadas > 0)
                {
                    resultado.Avisos.Add(Formatar(
                        "{0} viga(s) interna(s) sem montante/apoio correspondente dentro da tolerancia de {1:F1} mm " +
                        "nao foram alinhadas (permanecem na grade).",
                        internasNaoAlinhadas, toleranciaAlinhamentoMm));
                }
            }

            // 2. Segmentos de cada viga.
            foreach (PosicaoViga item in unicos)
            {
                List<SegmentoViga> segmentos;
                if (larguraVigaMm > 0.0)
                {
                    double posicaoFisica = PosicaoFisicaTransversalMm(orientacao, item.Posicao, larguraVigaMm);
                    segmentos = SegmentosFisicosDaViga(us, vs, posicaoFisica, larguraVigaMm);
                }
                else
                {
                    segmentos = SegmentosDaViga(us, vs, item.Posicao);
                }
                if (segmentos.Count == 0)
                {
                    string tipo = item.Borda ? "Viga de borda" : "Viga";
                    resultado.Avisos.Add(Formatar(
                        "{0} em v = {1:F1} mm nao intercepta o contorno e foi descartada.", tipo, item.Posicao));
                    continue;
                }

                resultado.Vigas.Add(new VigaLaje(item.Posicao, item.Grade, item.Alinhada, segmentos));
            }

            // 3. Vaos entre vigas.
            double maior = 0.0;
            for (int i = 0; i + 1 < resultado.Vigas.Count; i++)
            {
                double a = resultado.Vigas[i].PosicaoTransversalMm;
                double b = resultado.Vigas[i + 1].PosicaoTransversalMm;
                double vao = b - a;
                if (vao > maior) maior = vao;

                if (vao > espacamentoMm + ToleranciaMm)
                {
                    resultado.Avisos.Add(Formatar(
                        "Vao entre vigas de {0:F1} mm excede o espacamento de {1:F1} mm (entre v = {2:F1} e v = {3:F1} mm).",
                        vao, espacamentoMm, a, b));
                }

                if (larguraVigaMm > 0.0 && vao < larguraVigaMm)
                {
                    resultado.Avisos.Add(Formatar(
                        "Vao entre vigas de {0:F1} mm menor que a largura da viga ({1:F1} mm): as vigas se sobrepoem " +
                        "(entre v = {2:F1} e v = {3:F1} mm).",
                        vao, larguraVigaMm, a, b));
                }
            }

            resultado.MaiorVaoEntreVigasMm = maior;

            // 4. Guias de borda e arestas paralelas sem viga.
            double margem = Math.Max(ToleranciaMm, toleranciaAlinhamentoMm);
            var avisadas = new List<double>();

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = us[j] - us[i];
                double dv = vs[j] - vs[i];

                if (Math.Abs(du) <= ToleranciaMm && Math.Abs(dv) > ToleranciaMm)
                {
                    resultado.GuiasBorda.Add(new GuiaBordaLaje(
                        (us[i] + us[j]) / 2.0, Math.Min(vs[i], vs[j]), Math.Max(vs[i], vs[j])));
                }
                else if (Math.Abs(dv) <= ToleranciaMm && Math.Abs(du) > ToleranciaMm)
                {
                    double ve = (vs[i] + vs[j]) / 2.0;
                    if (TemVigaProxima(resultado.Vigas, ve, margem) || JaAvisada(avisadas, ve)) continue;

                    avisadas.Add(ve);
                    resultado.Avisos.Add(Formatar(
                        "Aresta paralela as vigas em v = {0:F1} mm sem viga de borda (nao tratado na v1).", ve));
                }
            }

            return resultado;
        }

        private static bool ArestasOrtogonais(double[] us, double[] vs)
        {
            int n = us.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (Math.Abs(us[j] - us[i]) > ToleranciaMm && Math.Abs(vs[j] - vs[i]) > ToleranciaMm) return false;
            }

            return true;
        }

        private static List<double> PosicoesDaGrade(double vMin, double vMax, double espacamentoMm)
        {
            var lista = new List<double> { vMin };

            for (int k = 1; ; k++)
            {
                double p = vMin + k * espacamentoMm;
                if (p >= vMax - ToleranciaMm) break;
                lista.Add(p);
            }

            if (vMax - lista[lista.Count - 1] > ToleranciaMm) lista.Add(vMax);

            return lista;
        }

        /// <summary>
        /// Alinha cada viga INTERNA ao apoio mais proximo dentro da tolerancia. Vigas de borda nao
        /// sao movidas. Devolve quantos apoios nao finitos foram ignorados.
        /// </summary>
        private static int AlinharAosApoios(
            List<PosicaoViga> itens, IEnumerable<double> apoios, double tolerancia, double vMin, double vMax)
        {
            int naoFinitos = 0;
            var validos = new List<double>();

            foreach (double a in apoios)
            {
                if (double.IsNaN(a) || double.IsInfinity(a))
                {
                    naoFinitos++;
                    continue;
                }

                if (a < vMin - ToleranciaMm || a > vMax + ToleranciaMm) continue;
                validos.Add(a);
            }

            validos.Sort();

            for (int i = 0; i < itens.Count; i++)
            {
                PosicaoViga item = itens[i];
                if (item.Borda) continue;

                double melhor = 0.0;
                double melhorDistancia = double.MaxValue;
                bool achou = false;

                foreach (double a in validos)
                {
                    double distancia = Math.Abs(a - item.Posicao);
                    if (distancia <= tolerancia && distancia < melhorDistancia)
                    {
                        melhor = a;
                        melhorDistancia = distancia;
                        achou = true;
                    }
                }

                if (achou)
                {
                    item.Posicao = Math.Min(Math.Max(melhor, vMin), vMax);
                    item.Alinhada = true;
                    itens[i] = item;
                }
            }

            return naoFinitos;
        }

        private static int CompararPosicao(PosicaoViga x, PosicaoViga y)
        {
            int c = x.Posicao.CompareTo(y.Posicao);
            return c != 0 ? c : x.Grade.CompareTo(y.Grade);
        }

        /// <summary>
        /// Resolve colisoes (posicoes a ate ToleranciaMm) entre vigas consecutivas, ja ordenadas.
        /// Quem fica: borda; depois alinhada sobre nao alinhada; depois menor deslocamento; por fim
        /// menor posicao de grade. Cada remocao gera aviso.
        /// </summary>
        private static List<PosicaoViga> RemoverColisoes(List<PosicaoViga> ordenados, List<string> avisos)
        {
            var mantidos = new List<PosicaoViga>();

            foreach (PosicaoViga atual in ordenados)
            {
                if (mantidos.Count == 0)
                {
                    mantidos.Add(atual);
                    continue;
                }

                int ultimo = mantidos.Count - 1;
                PosicaoViga anterior = mantidos[ultimo];

                if (Math.Abs(atual.Posicao - anterior.Posicao) > ToleranciaMm)
                {
                    mantidos.Add(atual);
                    continue;
                }

                if (Prefere(atual, anterior))
                {
                    avisos.Add(MensagemColisao(anterior, atual));
                    mantidos[ultimo] = atual;
                }
                else
                {
                    avisos.Add(MensagemColisao(atual, anterior));
                }
            }

            return mantidos;
        }

        private static bool Prefere(PosicaoViga a, PosicaoViga b)
        {
            if (a.Borda != b.Borda) return a.Borda;
            if (a.Alinhada != b.Alinhada) return a.Alinhada;

            double deslocamentoA = Math.Abs(a.Posicao - a.Grade);
            double deslocamentoB = Math.Abs(b.Posicao - b.Grade);
            if (Math.Abs(deslocamentoA - deslocamentoB) > 1e-9) return deslocamentoA < deslocamentoB;

            return a.Grade < b.Grade;
        }

        private static string MensagemColisao(PosicaoViga removida, PosicaoViga mantida)
        {
            if (mantida.Borda)
            {
                return Formatar(
                    "Viga em v = {0:F1} mm colide com a viga de borda em v = {1:F1} mm e foi removida; a borda foi preservada.",
                    removida.Posicao, mantida.Posicao);
            }

            if (removida.Alinhada && mantida.Alinhada)
            {
                return Formatar(
                    "Duas vigas foram alinhadas ao mesmo apoio (v = {0:F1} mm); foi mantida a de grade v = {1:F1} mm " +
                    "e removida a de grade v = {2:F1} mm (criterio: menor deslocamento, depois menor posicao de grade).",
                    mantida.Posicao, mantida.Grade, removida.Grade);
            }

            if (mantida.Alinhada)
            {
                return Formatar(
                    "Viga em v = {0:F1} mm removida por colidir com a viga alinhada ao apoio em v = {1:F1} mm.",
                    removida.Posicao, mantida.Posicao);
            }

            return Formatar(
                "Vigas coincidentes em v = {0:F1} mm; foi removida a de grade v = {1:F1} mm.",
                mantida.Posicao, removida.Grade);
        }

        /// <summary>
        /// Segmentos da viga na posicao v: uniao dos intervalos do contorno logo antes e logo depois
        /// da linha. Assim uma viga sobre uma aresta paralela a ela (borda ou degrau) usa o lado que
        /// tem material, e vigas em arestas de degrau cobrem o trecho mais longo.
        /// </summary>
        private static List<SegmentoViga> SegmentosDaViga(double[] us, double[] vs, double posicaoV)
        {
            var intervalos = new List<double[]>();
            AdicionarIntervalos(us, vs, posicaoV - DeslocamentoVarreduraMm, intervalos);
            AdicionarIntervalos(us, vs, posicaoV + DeslocamentoVarreduraMm, intervalos);
            return IntervalosParaSegmentos(intervalos);
        }

        /// <summary>
        /// Segmentos em que toda a pegada fisica da viga (v central +/- meia largura) esta dentro
        /// do contorno. Amostra as faixas de v separadas pelas arestas horizontais e intersecta os
        /// intervalos de u, de modo que degraus/entalhes nao sejam cobertos pela largura do perfil.
        /// </summary>
        private static List<SegmentoViga> SegmentosFisicosDaViga(
            double[] us, double[] vs, double posicaoFisicaV, double larguraVigaMm)
        {
            double meiaLargura = larguraVigaMm / 2.0;
            double vBaixo = posicaoFisicaV - meiaLargura;
            double vAlto = posicaoFisicaV + meiaLargura;
            if (vAlto - vBaixo <= 1e-9) return new List<SegmentoViga>();

            List<double> amostras = AmostrasFisicas(vs, vBaixo, vAlto);
            List<double[]> acumulado = null;

            foreach (double vAmostra in amostras)
            {
                var intervalos = new List<double[]>();
                AdicionarIntervalos(us, vs, vAmostra, intervalos);
                if (intervalos.Count == 0) return new List<SegmentoViga>();

                acumulado = acumulado == null
                    ? intervalos
                    : IntersectarIntervalos(acumulado, intervalos);

                if (acumulado.Count == 0) return new List<SegmentoViga>();
            }

            return acumulado == null ? new List<SegmentoViga>() : IntervalosParaSegmentos(acumulado);
        }

        private static List<double> AmostrasFisicas(double[] vs, double vBaixo, double vAlto)
        {
            double altura = vAlto - vBaixo;
            double epsilon = Math.Min(DeslocamentoVarreduraMm, altura / 4.0);
            var criticos = new List<double> { vBaixo, vAlto };

            foreach (double v in vs)
            {
                if (v > vBaixo + epsilon && v < vAlto - epsilon) criticos.Add(v);
            }

            criticos.Sort();
            var criticosUnicos = new List<double>();
            foreach (double v in criticos)
            {
                if (criticosUnicos.Count == 0 || Math.Abs(v - criticosUnicos[criticosUnicos.Count - 1]) > 1e-9)
                    criticosUnicos.Add(v);
            }

            var amostras = new List<double>();
            for (int i = 0; i < criticosUnicos.Count; i++)
            {
                if (i == 0) amostras.Add(criticosUnicos[i] + epsilon);
                else if (i == criticosUnicos.Count - 1) amostras.Add(criticosUnicos[i] - epsilon);
                else
                {
                    amostras.Add(criticosUnicos[i] - epsilon);
                    amostras.Add(criticosUnicos[i] + epsilon);
                }

                if (i + 1 < criticosUnicos.Count)
                    amostras.Add((criticosUnicos[i] + criticosUnicos[i + 1]) / 2.0);
            }

            amostras.Sort();

            var unicas = new List<double>();
            foreach (double amostra in amostras)
            {
                if (amostra < vBaixo || amostra > vAlto) continue;
                if (unicas.Count == 0 || Math.Abs(amostra - unicas[unicas.Count - 1]) > 1e-9)
                    unicas.Add(amostra);
            }

            return unicas;
        }

        private static List<double[]> IntersectarIntervalos(List<double[]> a, List<double[]> b)
        {
            var resultado = new List<double[]>();
            int i = 0;
            int j = 0;

            while (i < a.Count && j < b.Count)
            {
                double inicio = Math.Max(a[i][0], b[j][0]);
                double fim = Math.Min(a[i][1], b[j][1]);
                if (fim - inicio > ToleranciaMm) resultado.Add(new[] { inicio, fim });

                if (Math.Abs(a[i][1] - b[j][1]) <= 1e-9)
                {
                    i++;
                    j++;
                }
                else if (a[i][1] < b[j][1]) i++;
                else j++;
            }

            return resultado;
        }

        private static List<SegmentoViga> IntervalosParaSegmentos(List<double[]> intervalos)
        {
            intervalos.Sort((x, y) => x[0].CompareTo(y[0]));

            var segmentos = new List<SegmentoViga>();
            bool temAtual = false;
            double inicio = 0.0;
            double fim = 0.0;

            foreach (double[] intervalo in intervalos)
            {
                if (!temAtual)
                {
                    inicio = intervalo[0];
                    fim = intervalo[1];
                    temAtual = true;
                }
                else if (intervalo[0] <= fim + ToleranciaMm)
                {
                    fim = Math.Max(fim, intervalo[1]);
                }
                else
                {
                    if (fim - inicio > ToleranciaMm) segmentos.Add(new SegmentoViga(inicio, fim));
                    inicio = intervalo[0];
                    fim = intervalo[1];
                }
            }

            if (temAtual && fim - inicio > ToleranciaMm) segmentos.Add(new SegmentoViga(inicio, fim));

            return segmentos;
        }

        private static void AdicionarIntervalos(double[] us, double[] vs, double v0, List<double[]> intervalos)
        {
            var cruzamentos = new List<double>();
            int n = us.Length;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double v1 = vs[i];
                double v2 = vs[j];
                if (v1 == v2) continue;

                // Intervalo semiaberto [min, max): evita contar duas vezes o vertice compartilhado.
                if (v0 < Math.Min(v1, v2) || v0 >= Math.Max(v1, v2)) continue;

                double t = (v0 - v1) / (v2 - v1);
                cruzamentos.Add(us[i] + t * (us[j] - us[i]));
            }

            cruzamentos.Sort();

            for (int k = 0; k + 1 < cruzamentos.Count; k += 2)
            {
                intervalos.Add(new[] { cruzamentos[k], cruzamentos[k + 1] });
            }
        }

        private static bool TemVigaProxima(List<VigaLaje> vigas, double v, double margem)
        {
            foreach (VigaLaje viga in vigas)
            {
                if (Math.Abs(viga.PosicaoTransversalMm - v) <= margem) return true;
            }

            return false;
        }

        private static bool JaAvisada(List<double> avisadas, double v)
        {
            foreach (double a in avisadas)
            {
                if (Math.Abs(a - v) <= ToleranciaMm) return true;
            }

            return false;
        }

        private static string Formatar(string formato, params object[] argumentos)
        {
            return string.Format(CultureInfo.InvariantCulture, formato, argumentos);
        }
    }
}
