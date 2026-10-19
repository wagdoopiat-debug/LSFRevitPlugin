using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Contorno da laje na planta: poligono simples fechado, de lados retos, em milimetros.
    /// Ao construir, remove o ponto de fechamento repetido e pontos consecutivos coincidentes,
    /// e deixa os pontos SEMPRE em sentido anti-horario (area assinada positiva).
    /// So descreve a planta: nao carrega cota nem unidade interna do Revit.
    /// </summary>
    public sealed class ContornoLaje
    {
        /// <summary>Distancia (mm) abaixo da qual dois pontos sao considerados o mesmo.</summary>
        public const double ToleranciaMm = 0.001;

        /// <summary>Area minima (mm2) para o contorno ser considerado valido.</summary>
        public const double AreaMinimaMm2 = 1.0;

        /// <summary>
        /// Distancia (mm) abaixo da qual dois extremos de borda sao tratados como o mesmo vertice
        /// ao montar um contorno a partir de segmentos. Um pouco maior que ToleranciaMm para
        /// absorver arredondamento de conversao de unidades sem juntar vertices realmente distintos.
        /// </summary>
        public const double ToleranciaJuncaoMm = 0.1;

        // Tolerancia do produto vetorial (mm2) ao decidir se tres pontos sao colineares.
        private const double ToleranciaCruzadoMm2 = 1e-6;

        private readonly ReadOnlyCollection<Ponto2DMm> _pontos;

        public ContornoLaje(IEnumerable<Ponto2DMm> pontos)
        {
            if (pontos == null) throw new ArgumentNullException(nameof(pontos));

            List<Ponto2DMm> lista = LimparPontos(pontos);
            if (CalcularAreaAssinada(lista) < 0.0) lista.Reverse();

            _pontos = lista.AsReadOnly();
            AreaAssinadaMm2 = CalcularAreaAssinada(lista);
        }

        /// <summary>
        /// Monta um contorno fechado a partir de bordas soltas (linhas de modelo/detalhe
        /// selecionadas pelo usuario ou trechos desenhados por pontos). Une extremos proximos no
        /// mesmo vertice, exige que cada vertice tenha exatamente duas bordas e que todas formem
        /// um unico ciclo. Devolve null e preenche <paramref name="erros"/> quando nao fecha
        /// (borda solta, ramificacao, mais de um contorno) ou quando o poligono resultante e
        /// invalido (autointerseccao, area nula).
        /// </summary>
        public static ContornoLaje MontarDeSegmentos(
            IEnumerable<SegmentoContorno> segmentos, out List<string> erros)
        {
            erros = new List<string>();

            if (segmentos == null)
            {
                erros.Add("Nenhuma borda informada para montar o contorno.");
                return null;
            }

            var validos = new List<SegmentoContorno>();
            foreach (SegmentoContorno s in segmentos)
            {
                if (s.EhDegenerado)
                {
                    erros.Add("Borda com comprimento desprezivel foi descartada.");
                    continue;
                }

                validos.Add(s);
            }

            if (validos.Count < 3)
            {
                erros.Add("Sao necessarias pelo menos 3 bordas com comprimento para fechar o contorno.");
                return null;
            }

            // Junta extremos proximos no mesmo vertice (tolerancia de juncao).
            var vertices = new List<Ponto2DMm>();
            int VerticeDe(Ponto2DMm p)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (vertices[i].EstaProximoDe(p, ToleranciaJuncaoMm)) return i;
                }

                vertices.Add(p);
                return vertices.Count - 1;
            }

            var arestas = new List<int[]>();
            foreach (SegmentoContorno s in validos)
            {
                int a = VerticeDe(s.Inicio);
                int b = VerticeDe(s.Fim);
                if (a == b)
                {
                    erros.Add("Borda com comprimento desprezivel foi descartada.");
                    continue;
                }

                arestas.Add(new[] { a, b });
            }

            if (arestas.Count < 3)
            {
                erros.Add("Sao necessarias pelo menos 3 bordas com comprimento para fechar o contorno.");
                return null;
            }

            var grau = new int[vertices.Count];
            foreach (int[] aresta in arestas)
            {
                grau[aresta[0]]++;
                grau[aresta[1]]++;
            }

            for (int v = 0; v < grau.Length; v++)
            {
                if (grau[v] == 1)
                {
                    erros.Add("As bordas selecionadas formam um contorno aberto (extremidade livre).");
                    return null;
                }

                if (grau[v] > 2)
                {
                    erros.Add("As bordas selecionadas se ramificam (mais de duas bordas no mesmo vertice).");
                    return null;
                }
            }

            // Encadeia a partir da primeira aresta; exige um unico ciclo que use todas as bordas.
            var usada = new bool[arestas.Count];
            var ordem = new List<int> { arestas[0][0], arestas[0][1] };
            usada[0] = true;
            int atual = arestas[0][1];

            for (int passo = 1; passo < arestas.Count; passo++)
            {
                int escolhida = -1;
                int outro = -1;
                for (int i = 0; i < arestas.Count; i++)
                {
                    if (usada[i]) continue;

                    if (arestas[i][0] == atual)
                    {
                        escolhida = i;
                        outro = arestas[i][1];
                        break;
                    }

                    if (arestas[i][1] == atual)
                    {
                        escolhida = i;
                        outro = arestas[i][0];
                        break;
                    }
                }

                if (escolhida < 0)
                {
                    erros.Add("As bordas selecionadas nao formam um unico contorno fechado "
                        + "(existem bordas soltas ou mais de um contorno).");
                    return null;
                }

                usada[escolhida] = true;
                ordem.Add(outro);
                atual = outro;
            }

            if (atual != ordem[0])
            {
                erros.Add("As bordas selecionadas nao fecham o contorno (o fim nao volta ao inicio).");
                return null;
            }

            ordem.RemoveAt(ordem.Count - 1); // remove repeticao do ponto inicial

            var pontos = new List<Ponto2DMm>(ordem.Count);
            foreach (int v in ordem) pontos.Add(vertices[v]);

            ContornoLaje contorno = new ContornoLaje(pontos);
            erros.AddRange(contorno.Validar());
            return contorno;
        }

        /// <summary>Pontos em sentido anti-horario, sem repetir o ponto de fechamento.</summary>
        public IReadOnlyList<Ponto2DMm> Pontos => _pontos;

        /// <summary>Area com sinal, em mm2. Depois da normalizacao e positiva (ou zero se degenerado).</summary>
        public double AreaAssinadaMm2 { get; }

        public double AreaMm2 => Math.Abs(AreaAssinadaMm2);

        public double AreaM2 => AreaMm2 / 1000000.0;

        public bool EhValido => Validar().Count == 0;

        /// <summary>
        /// Erros que impedem o uso do contorno (lista vazia = valido): menos de 3 pontos
        /// distintos, autointerseccao (cruza, toca ou volta sobre si mesmo) e area quase nula.
        /// </summary>
        public List<string> Validar()
        {
            var erros = new List<string>();

            if (_pontos.Count < 3)
            {
                erros.Add("Contorno precisa de pelo menos 3 pontos distintos.");
                return erros;
            }

            if (TemAutointerseccao())
            {
                erros.Add("Contorno se cruza, se toca ou volta sobre si mesmo (autointerseccao).");
                return erros;
            }

            if (AreaMm2 < AreaMinimaMm2)
            {
                erros.Add("Contorno com area zero ou desprezivel.");
            }

            return erros;
        }

        private static List<Ponto2DMm> LimparPontos(IEnumerable<Ponto2DMm> pontos)
        {
            var resultado = new List<Ponto2DMm>();

            foreach (Ponto2DMm p in pontos)
            {
                if (resultado.Count > 0 && resultado[resultado.Count - 1].EstaProximoDe(p, ToleranciaMm))
                {
                    continue;
                }

                resultado.Add(p);
            }

            // Ponto de fechamento repetido no fim.
            while (resultado.Count > 1 && resultado[0].EstaProximoDe(resultado[resultado.Count - 1], ToleranciaMm))
            {
                resultado.RemoveAt(resultado.Count - 1);
            }

            return resultado;
        }

        /// <summary>
        /// Area com sinal por triangulos a partir do primeiro ponto (valido para poligonos
        /// simples, inclusive concavos). Positiva = anti-horario.
        /// </summary>
        private static double CalcularAreaAssinada(IReadOnlyList<Ponto2DMm> pts)
        {
            if (pts.Count < 3) return 0.0;

            Ponto2DMm origem = pts[0];
            double soma = 0.0;

            for (int i = 1; i < pts.Count - 1; i++)
            {
                double ax = pts[i].XMm - origem.XMm;
                double ay = pts[i].YMm - origem.YMm;
                double bx = pts[i + 1].XMm - origem.XMm;
                double by = pts[i + 1].YMm - origem.YMm;
                soma += ax * by - ay * bx;
            }

            return soma / 2.0;
        }

        private bool TemAutointerseccao()
        {
            int n = _pontos.Count;

            // Arestas consecutivas: so se sobrepoem se uma volta sobre a outra.
            for (int k = 0; k < n; k++)
            {
                if (VoltaSobreSi(_pontos[k], _pontos[(k + 1) % n], _pontos[(k + 2) % n])) return true;
            }

            // Arestas nao consecutivas: nao podem se cruzar nem se tocar.
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue; // consecutivas pelo fechamento

                    if (SegmentosSeTocam(
                        _pontos[i], _pontos[(i + 1) % n],
                        _pontos[j], _pontos[(j + 1) % n]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SegmentosSeTocam(Ponto2DMm p1, Ponto2DMm q1, Ponto2DMm p2, Ponto2DMm q2)
        {
            int o1 = Sinal(Cruzado(p1, q1, p2));
            int o2 = Sinal(Cruzado(p1, q1, q2));
            int o3 = Sinal(Cruzado(p2, q2, p1));
            int o4 = Sinal(Cruzado(p2, q2, q1));

            if (o1 != o2 && o3 != o4) return true;

            if (o1 == 0 && DentroDaCaixa(p1, q1, p2)) return true;
            if (o2 == 0 && DentroDaCaixa(p1, q1, q2)) return true;
            if (o3 == 0 && DentroDaCaixa(p2, q2, p1)) return true;
            if (o4 == 0 && DentroDaCaixa(p2, q2, q1)) return true;

            return false;
        }

        /// <summary>
        /// Duas arestas que se encontram em "comum" voltam uma sobre a outra quando sao
        /// colineares e saem do ponto comum na mesma direcao.
        /// </summary>
        private static bool VoltaSobreSi(Ponto2DMm anterior, Ponto2DMm comum, Ponto2DMm seguinte)
        {
            double v1x = anterior.XMm - comum.XMm;
            double v1y = anterior.YMm - comum.YMm;
            double v2x = seguinte.XMm - comum.XMm;
            double v2y = seguinte.YMm - comum.YMm;

            double cruzado = v1x * v2y - v1y * v2x;
            double escalar = v1x * v2x + v1y * v2y;

            return Sinal(cruzado) == 0 && escalar > 0.0;
        }

        private static double Cruzado(Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            return (b.XMm - a.XMm) * (c.YMm - a.YMm) - (b.YMm - a.YMm) * (c.XMm - a.XMm);
        }

        private static int Sinal(double valor)
        {
            if (valor > ToleranciaCruzadoMm2) return 1;
            if (valor < -ToleranciaCruzadoMm2) return -1;
            return 0;
        }

        private static bool DentroDaCaixa(Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            return c.XMm >= Math.Min(a.XMm, b.XMm) - ToleranciaMm
                && c.XMm <= Math.Max(a.XMm, b.XMm) + ToleranciaMm
                && c.YMm >= Math.Min(a.YMm, b.YMm) - ToleranciaMm
                && c.YMm <= Math.Max(a.YMm, b.YMm) + ToleranciaMm;
        }
    }
}
