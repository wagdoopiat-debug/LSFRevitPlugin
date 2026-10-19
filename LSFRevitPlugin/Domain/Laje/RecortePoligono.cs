using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Utilidades puras de poligonos 2D em milimetros, compartilhadas pelo parcelamento das
    /// placas e pela modelagem 3D da telha: convexidade, area, ponto-dentro, recorte de poligono
    /// (Sutherland-Hodgman), varredura vertical (intervalos em v) e triangulacao (ear clipping).
    /// Sem dependencia do Revit, para ser testado sem abrir o Revit.
    /// </summary>
    public static class RecortePoligono
    {
        /// <summary>Area (mm2) do poligono, pelo metodo do cadarco. Sempre positiva.</summary>
        public static double Area(IReadOnlyList<Ponto2DMm> pontos)
        {
            if (pontos == null || pontos.Count < 3) return 0.0;

            double soma = 0.0;
            for (int i = 0; i < pontos.Count; i++)
            {
                Ponto2DMm a = pontos[i];
                Ponto2DMm b = pontos[(i + 1) % pontos.Count];
                soma += a.XMm * b.YMm - b.XMm * a.YMm;
            }

            return Math.Abs(soma) / 2.0;
        }

        /// <summary>Area com sinal (positiva = anti-horario).</summary>
        public static double AreaAssinada(IReadOnlyList<Ponto2DMm> pontos)
        {
            if (pontos == null || pontos.Count < 3) return 0.0;

            double soma = 0.0;
            for (int i = 0; i < pontos.Count; i++)
            {
                Ponto2DMm a = pontos[i];
                Ponto2DMm b = pontos[(i + 1) % pontos.Count];
                soma += a.XMm * b.YMm - b.XMm * a.YMm;
            }

            return soma / 2.0;
        }

        /// <summary>true se o poligono e convexo (todos os vertices com a mesma orientacao).</summary>
        public static bool EhConvexo(IReadOnlyList<Ponto2DMm> pontos)
        {
            int n = pontos?.Count ?? 0;
            if (n < 3) return false;

            int sinal = 0;
            for (int i = 0; i < n; i++)
            {
                Ponto2DMm a = pontos[i];
                Ponto2DMm b = pontos[(i + 1) % n];
                Ponto2DMm c = pontos[(i + 2) % n];

                double cruzado = Cruzado(a, b, c);
                if (Math.Abs(cruzado) < 1e-6) continue;

                int s = cruzado > 0.0 ? 1 : -1;
                if (sinal != 0 && s != sinal) return false;
                sinal = s;
            }

            return sinal != 0;
        }

        /// <summary>true se o poligono esta em sentido anti-horario (area assinada positiva).</summary>
        public static bool EhAntiHorario(IReadOnlyList<Ponto2DMm> pontos)
        {
            return AreaAssinada(pontos) > 0.0;
        }

        /// <summary>Ponto dentro do poligono simples (ray casting). Borda conta como dentro.</summary>
        public static bool Contem(IReadOnlyList<Ponto2DMm> contorno, Ponto2DMm p)
        {
            if (contorno == null || contorno.Count < 3) return false;

            int n = contorno.Count;

            // Sobre a borda (dentro): evita resposta ambigua em recortes exatos.
            for (int i = 0; i < n; i++)
            {
                Ponto2DMm a = contorno[i];
                Ponto2DMm b = contorno[(i + 1) % n];
                if (PontoNoSegmento(p, a, b)) return true;
            }

            bool dentro = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = contorno[i].XMm;
                double yi = contorno[i].YMm;
                double xj = contorno[j].XMm;
                double yj = contorno[j].YMm;

                bool cruza = (yi > p.YMm) != (yj > p.YMm)
                    && p.XMm < (xj - xi) * (p.YMm - yi) / (yj - yi) + xi;
                if (cruza) dentro = !dentro;
            }

            return dentro;
        }

        /// <summary>
        /// Intervalos em v (transversais) em que a reta vertical u = <paramref name="u"/> esta
        /// dentro do poligono simples. Cada par (inicio, fim) tem inicio &lt; fim. Lista vazia =
        /// a reta nao corta o poligono. Funciona para contornos convexos ou concavos.
        /// </summary>
        public static List<(double Inicio, double Fim)> IntervalosV(
            IReadOnlyList<Ponto2DMm> contorno, double u)
        {
            var resultado = new List<(double Inicio, double Fim)>();
            if (contorno == null || contorno.Count < 3) return resultado;

            var cortes = new List<double>();
            int n = contorno.Count;

            for (int i = 0; i < n; i++)
            {
                Ponto2DMm a = contorno[i];
                Ponto2DMm b = contorno[(i + 1) % n];

                double lo = Math.Min(a.XMm, b.XMm);
                double hi = Math.Max(a.XMm, b.XMm);
                if (u < lo || u >= hi) continue;
                if (Math.Abs(b.XMm - a.XMm) < 1e-9) continue;

                double t = (u - a.XMm) / (b.XMm - a.XMm);
                cortes.Add(a.YMm + t * (b.YMm - a.YMm));
            }

            cortes.Sort();
            for (int k = 0; k + 1 < cortes.Count; k += 2)
            {
                if (cortes[k + 1] - cortes[k] > 1e-6)
                    resultado.Add((cortes[k], cortes[k + 1]));
            }

            return resultado;
        }

        /// <summary>
        /// Recorta <paramref name="sujeito"/> por <paramref name="janelaConvexa"/> via
        /// Sutherland-Hodgman. A JANELA precisa ser convexa e anti-horaria; o sujeito pode ser
        /// concavo. Quando a intersecao e desconexa, o resultado pode ser um unico poligono
        /// "administrativo" que liga os pedacos; para contornos concavos use
        /// <see cref="RecortarContornoPorCelula"/>.
        /// </summary>
        public static List<Ponto2DMm> Recortar(
            IReadOnlyList<Ponto2DMm> sujeito, IReadOnlyList<Ponto2DMm> janelaConvexa)
        {
            var saida = new List<Ponto2DMm>(sujeito ?? new List<Ponto2DMm>());
            if (janelaConvexa == null) return saida;

            int n = janelaConvexa.Count;
            for (int i = 0; i < n && saida.Count > 0; i++)
            {
                Ponto2DMm a = janelaConvexa[i];
                Ponto2DMm b = janelaConvexa[(i + 1) % n];
                List<Ponto2DMm> entrada = saida;
                saida = new List<Ponto2DMm>();

                Ponto2DMm anterior = entrada[entrada.Count - 1];
                bool anteriorDentro = Dentro(anterior, a, b);

                foreach (Ponto2DMm atual in entrada)
                {
                    bool atualDentro = Dentro(atual, a, b);

                    if (atualDentro)
                    {
                        if (!anteriorDentro) saida.Add(Intersecao(anterior, atual, a, b));
                        saida.Add(atual);
                    }
                    else if (anteriorDentro)
                    {
                        saida.Add(Intersecao(anterior, atual, a, b));
                    }

                    anterior = atual;
                    anteriorDentro = atualDentro;
                }
            }

            return LimparRepetidos(saida);
        }

        /// <summary>
        /// Recorta o contorno (convexo ou concavo) por uma celula retangular convexa e devolve os
        /// pedacos resultantes. Contorno convexo: um unico recorte direto. Contorno concavo:
        /// triangula o contorno e recorta cada triangulo pela celula, depois COSTURA os pedacos
        /// pelas arestas internas comuns, de modo que uma intersecao desconexa vira varios
        /// poligonos (e nao um poligono invalido). Conveniencia; para muitas celulas, prefira
        /// pre-calcular os triangulos com <see cref="TriangulosConvexos"/> e chamar
        /// <see cref="RecortarConvexosPorCelula"/>.
        /// </summary>
        public static List<List<Ponto2DMm>> RecortarContornoPorCelula(
            IReadOnlyList<Ponto2DMm> contorno, IReadOnlyList<Ponto2DMm> celulaConvexa,
            double areaMinimaMm2 = 1e-6)
        {
            return RecortarConvexosPorCelula(
                TriangulosConvexos(contorno), celulaConvexa, areaMinimaMm2);
        }

        /// <summary>
        /// Decompoe o contorno em pedacos convexos: o proprio contorno se for convexo; caso
        /// contrario, os triangulos do ear clipping. Reutilizavel entre celulas.
        /// </summary>
        public static List<List<Ponto2DMm>> TriangulosConvexos(IReadOnlyList<Ponto2DMm> contorno)
        {
            var lista = new List<List<Ponto2DMm>>();
            if (contorno == null || contorno.Count < 3) return lista;

            if (EhConvexo(contorno))
            {
                lista.Add(new List<Ponto2DMm>(contorno));
                return lista;
            }

            return Triangular(contorno);
        }

        /// <summary>
        /// Recorta cada pedaco convexo pela celula e costura os recortes pelas arestas internas
        /// comuns, devolvendo os poligonos resultantes (um por componente conexo).
        /// </summary>
        public static List<List<Ponto2DMm>> RecortarConvexosPorCelula(
            IReadOnlyList<List<Ponto2DMm>> convexos, IReadOnlyList<Ponto2DMm> celulaConvexa,
            double areaMinimaMm2 = 1e-6)
        {
            var recortes = new List<List<Ponto2DMm>>();
            if (convexos == null || celulaConvexa == null || celulaConvexa.Count < 3) return recortes;

            foreach (List<Ponto2DMm> convexo in convexos)
            {
                List<Ponto2DMm> recorte = Recortar(convexo, celulaConvexa);
                if (recorte.Count >= 3 && Area(recorte) >= areaMinimaMm2) recortes.Add(recorte);
            }

            if (recortes.Count <= 1) return recortes;

            return Costurar(recortes, areaMinimaMm2);
        }

        /// <summary>
        /// Costura varios poligonos que compartilham arestas (como os recortes dos triangulos de
        /// uma mesma regiao): arestas que aparecem nos dois sentidos sao internas e somem; as que
        /// sobram formam as bordas dos componentes conexos.
        /// </summary>
        public static List<List<Ponto2DMm>> Costurar(
            IReadOnlyList<List<Ponto2DMm>> poligonos, double areaMinimaMm2 = 1e-6)
        {
            var resultado = new List<List<Ponto2DMm>>();
            if (poligonos == null) return resultado;

            var contagem = new Dictionary<((long, long) A, (long, long) B), int>();
            var arestas = new List<(Ponto2DMm A, Ponto2DMm B, (long, long) Ka, (long, long) Kb)>();

            foreach (List<Ponto2DMm> poly in poligonos)
            {
                List<Ponto2DMm> limpo = LimparRepetidos(poly);
                if (limpo.Count < 3) continue;

                for (int i = 0; i < limpo.Count; i++)
                {
                    Ponto2DMm a = limpo[i];
                    Ponto2DMm b = limpo[(i + 1) % limpo.Count];
                    (long, long) ka = Chave(a);
                    (long, long) kb = Chave(b);
                    if (ka == kb) continue;

                    arestas.Add((a, b, ka, kb));
                    contagem.TryGetValue((ka, kb), out int c);
                    contagem[(ka, kb)] = c + 1;
                }
            }

            var mantidas = new List<(Ponto2DMm A, Ponto2DMm B, (long, long) Ka, (long, long) Kb)>();
            foreach (var e in arestas)
            {
                contagem.TryGetValue((e.Ka, e.Kb), out int atual);
                if (atual <= 0) continue;

                if (contagem.TryGetValue((e.Kb, e.Ka), out int reversa) && reversa > 0)
                {
                    contagem[(e.Ka, e.Kb)] = atual - 1;
                    contagem[(e.Kb, e.Ka)] = reversa - 1;
                    continue;
                }

                mantidas.Add(e);
            }

            var saida = new Dictionary<(long, long), List<int>>();
            for (int i = 0; i < mantidas.Count; i++)
            {
                if (!saida.TryGetValue(mantidas[i].Ka, out List<int> lista))
                {
                    lista = new List<int>();
                    saida[mantidas[i].Ka] = lista;
                }

                lista.Add(i);
            }

            var usado = new bool[mantidas.Count];
            for (int inicio = 0; inicio < mantidas.Count; inicio++)
            {
                if (usado[inicio]) continue;

                var loop = new List<Ponto2DMm> { mantidas[inicio].A, mantidas[inicio].B };
                usado[inicio] = true;
                (long, long) alvo = mantidas[inicio].Ka;
                (long, long) atual = mantidas[inicio].Kb;

                int guarda = 0;
                while (atual != alvo && guarda++ < mantidas.Count + 1)
                {
                    int proximo = -1;
                    if (saida.TryGetValue(atual, out List<int> candidatos))
                    {
                        foreach (int idx in candidatos)
                        {
                            if (!usado[idx])
                            {
                                proximo = idx;
                                break;
                            }
                        }
                    }

                    if (proximo < 0) break;

                    usado[proximo] = true;
                    loop.Add(mantidas[proximo].B);
                    atual = mantidas[proximo].Kb;
                }

                List<Ponto2DMm> limpo = LimparRepetidos(loop);
                if (limpo.Count >= 3 && Area(limpo) >= areaMinimaMm2) resultado.Add(limpo);
            }

            return resultado;
        }

        private static (long, long) Chave(Ponto2DMm p)
        {
            return ((long)Math.Round(p.XMm * 10000.0), (long)Math.Round(p.YMm * 10000.0));
        }

        /// <summary>
        /// Triangula um poligono simples (convexo ou concavo) por ear clipping. Devolve uma lista
        /// de triangulos (listas de 3 pontos), em coordenadas iguais as de entrada. Para um
        /// poligono degenerado devolve o que conseguir antes de travar.
        /// </summary>
        public static List<List<Ponto2DMm>> Triangular(IReadOnlyList<Ponto2DMm> contorno)
        {
            var triangulos = new List<List<Ponto2DMm>>();
            if (contorno == null || contorno.Count < 3) return triangulos;

            var poly = new List<Ponto2DMm>(contorno);
            if (!EhAntiHorario(poly)) poly.Reverse();

            int guarda = 0;
            while (poly.Count > 3 && guarda++ < 100000)
            {
                bool achouOrelha = false;
                int n = poly.Count;

                for (int i = 0; i < n; i++)
                {
                    Ponto2DMm a = poly[(i - 1 + n) % n];
                    Ponto2DMm b = poly[i];
                    Ponto2DMm c = poly[(i + 1) % n];

                    if (Cruzado(a, b, c) <= 1e-9) continue;

                    bool contemVertice = false;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || j == (i - 1 + n) % n || j == (i + 1) % n) continue;
                        if (PontoDentroDoTriangulo(poly[j], a, b, c))
                        {
                            contemVertice = true;
                            break;
                        }
                    }

                    if (contemVertice) continue;

                    triangulos.Add(new List<Ponto2DMm> { a, b, c });
                    poly.RemoveAt(i);
                    achouOrelha = true;
                    break;
                }

                if (!achouOrelha) break;
            }

            if (poly.Count == 3) triangulos.Add(new List<Ponto2DMm>(poly));
            return triangulos;
        }

        /// <summary>Remove pontos repetidos consecutivos (e o fechamento) de uma lista.</summary>
        public static List<Ponto2DMm> LimparRepetidos(IReadOnlyList<Ponto2DMm> pontos)
        {
            var limpos = new List<Ponto2DMm>(pontos?.Count ?? 0);
            if (pontos == null) return limpos;

            foreach (Ponto2DMm p in pontos)
            {
                if (limpos.Count > 0 && limpos[limpos.Count - 1].EstaProximoDe(p, 1e-6)) continue;
                limpos.Add(p);
            }

            while (limpos.Count > 1 && limpos[0].EstaProximoDe(limpos[limpos.Count - 1], 1e-6))
                limpos.RemoveAt(limpos.Count - 1);

            return limpos;
        }

        private static bool Dentro(Ponto2DMm p, Ponto2DMm a, Ponto2DMm b)
        {
            double cruzado = (b.XMm - a.XMm) * (p.YMm - a.YMm) - (b.YMm - a.YMm) * (p.XMm - a.XMm);
            return cruzado >= -1e-9;
        }

        private static Ponto2DMm Intersecao(Ponto2DMm p, Ponto2DMm q, Ponto2DMm a, Ponto2DMm b)
        {
            double rx = q.XMm - p.XMm;
            double ry = q.YMm - p.YMm;
            double sx = b.XMm - a.XMm;
            double sy = b.YMm - a.YMm;

            double denominador = rx * sy - ry * sx;
            if (Math.Abs(denominador) < 1e-12) return p;

            double t = ((a.XMm - p.XMm) * sy - (a.YMm - p.YMm) * sx) / denominador;
            return new Ponto2DMm(p.XMm + t * rx, p.YMm + t * ry);
        }

        private static double Cruzado(Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            return (b.XMm - a.XMm) * (c.YMm - a.YMm) - (b.YMm - a.YMm) * (c.XMm - a.XMm);
        }

        private static bool PontoNoSegmento(Ponto2DMm p, Ponto2DMm a, Ponto2DMm b)
        {
            double cruzado = (b.XMm - a.XMm) * (p.YMm - a.YMm) - (b.YMm - a.YMm) * (p.XMm - a.XMm);
            if (Math.Abs(cruzado) > 1e-6) return false;

            return p.XMm >= Math.Min(a.XMm, b.XMm) - 1e-6
                && p.XMm <= Math.Max(a.XMm, b.XMm) + 1e-6
                && p.YMm >= Math.Min(a.YMm, b.YMm) - 1e-6
                && p.YMm <= Math.Max(a.YMm, b.YMm) + 1e-6;
        }

        private static bool PontoDentroDoTriangulo(
            Ponto2DMm p, Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            double d1 = Cruzado(a, b, p);
            double d2 = Cruzado(b, c, p);
            double d3 = Cruzado(c, a, p);

            bool temNeg = d1 < -1e-9 || d2 < -1e-9 || d3 < -1e-9;
            bool temPos = d1 > 1e-9 || d2 > 1e-9 || d3 > 1e-9;

            return !(temNeg && temPos);
        }
    }
}
