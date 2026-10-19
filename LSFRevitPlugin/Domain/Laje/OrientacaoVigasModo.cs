using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Modo de escolha da direcao das vigas. Automatica delega para OrientacaoVigas (lado
    /// mais curto do contorno). Manual usa o angulo informado. Paralela/Perpendicular usam uma
    /// aresta do contorno escolhida pelo usuario como referencia, desenhando a direcao da
    /// aresta ou a dela cruzada em 90 graus.
    /// </summary>
    public enum OrientacaoVigasModo
    {
        Automatica,
        ParalelaAresta,
        PerpendicularAresta,
        Manual
    }

    /// <summary>
    /// Representacao simplificada da orientacao das vigas para UI.
    /// Unifica ParalelaAresta e PerpendicularAresta para uma unica escolha "Por borda de referencia".
    /// </summary>
    public enum OrientacaoUI
    {
        Automatica,
        PorBordaReferencia,
        Manual
    }

    /// <summary>
    /// Converte o modo de orientacao escolhido pelo usuario em um angulo (graus) que a
    /// OrientacaoVigas.Calcular ja entende, ou em null para a direcao automatica. Puro (sem
    /// Revit), para poder ser testado sem abrir o Revit e reaproveitado pela UI, pela previa
    /// e pela geracao.
    /// </summary>
    public static class ResolvedorOrientacaoVigas
    {
        public static string NomeDoModo(OrientacaoVigasModo modo)
        {
            switch (modo)
            {
                case OrientacaoVigasModo.ParalelaAresta: return "paralela a uma borda";
                case OrientacaoVigasModo.PerpendicularAresta: return "perpendicular a uma borda";
                case OrientacaoVigasModo.Manual: return "manual (graus)";
                default: return "automatica (lado mais curto)";
            }
        }

        /// <summary>
        /// Devolve o angulo (graus) a passar para a OrientacaoVigas, ou null quando a direcao
        /// e automatica. <paramref name="erros"/> recebe as falhas (lista vazia = sucesso).
        /// </summary>
        public static double? DirecaoGraus(
            ContornoLaje contorno,
            OrientacaoVigasModo modo,
            int arestaReferencia,
            double? grausManuais,
            out List<string> erros)
        {
            erros = new List<string>();

            if (contorno == null)
            {
                erros.Add("Contorno nao informado.");
                return null;
            }

            switch (modo)
            {
                case OrientacaoVigasModo.Manual:
                    if (!grausManuais.HasValue
                        || double.IsNaN(grausManuais.Value)
                        || double.IsInfinity(grausManuais.Value))
                    {
                        erros.Add("Direcao manual deve ser um numero finito de graus.");
                        return null;
                    }

                    return Normalizar180(grausManuais.Value);

                case OrientacaoVigasModo.ParalelaAresta:
                case OrientacaoVigasModo.PerpendicularAresta:
                    double? anguloAresta = AnguloDaAresta(contorno, arestaReferencia, out erros);
                    if (!anguloAresta.HasValue) return null;

                    return modo == OrientacaoVigasModo.ParalelaAresta
                        ? anguloAresta.Value
                        : Normalizar180(anguloAresta.Value + 90.0);

                default:
                    return null; // automatica
            }
        }

        /// <summary>Angulo (graus, [0,180)) de uma aresta do contorno, ou null com erro registrado.</summary>
        public static double? AnguloDaAresta(
            ContornoLaje contorno, int arestaReferencia, out List<string> erros)
        {
            erros = new List<string>();

            if (contorno == null)
            {
                erros.Add("Contorno nao informado.");
                return null;
            }

            int n = contorno.Pontos.Count;
            if (arestaReferencia < 0 || arestaReferencia >= n)
            {
                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Borda de referencia {0} invalida (o contorno tem {1} bordas, de 0 a {2}).",
                    arestaReferencia, n, n - 1));
                return null;
            }

            Ponto2DMm a = contorno.Pontos[arestaReferencia];
            Ponto2DMm b = contorno.Pontos[(arestaReferencia + 1) % n];

            double dx = b.XMm - a.XMm;
            double dy = b.YMm - a.YMm;

            if (Math.Sqrt(dx * dx + dy * dy) <= ContornoLaje.ToleranciaMm)
            {
                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Borda de referencia {0} tem comprimento desprezivel.", arestaReferencia));
                return null;
            }

            return Normalizar180(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        /// <summary>Descricao curta e estavel da direcao escolhida, para mostrar no resumo.</summary>
        public static string DescreverDirecao(double? direcaoGraus)
        {
            return direcaoGraus.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:F1} graus", direcaoGraus.Value)
                : "automatica";
        }

        public static double Normalizar180(double graus)
        {
            double a = graus % 180.0;
            if (a < 0.0) a += 180.0;
            if (a >= 180.0) a -= 180.0;
            return a;
        }
    }
}
