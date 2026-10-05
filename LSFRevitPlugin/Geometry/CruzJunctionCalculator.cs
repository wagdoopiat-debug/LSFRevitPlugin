using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>Deteccao e calculo do reforco Quadruplo em encontros de parede em + (meio-meio).</summary>
    public static class CruzJunctionCalculator
    {
        private const string PrefixoQuadruplo = "LSF-Quadruplo";

        public static List<CruzJunctionInfo> DetectarEncontrosEmCruz(Document doc, Wall paredeSelecionada)
        {
            var resultado = new List<CruzJunctionInfo>();

            LocationCurve minhaCurva = paredeSelecionada.Location as LocationCurve;
            if (minhaCurva == null || !(minhaCurva.Curve is Line minhaLinha))
                throw new InvalidOperationException("Parede selecionada nao possui eixo reto valido.");

            XYZ minhaDirecao = (minhaLinha.GetEndPoint(1) - minhaLinha.GetEndPoint(0)).Normalize();
            double meuParamInicio = minhaLinha.GetEndParameter(0);
            double meuParamFim = minhaLinha.GetEndParameter(1);
            double margemPontas = WallGeometryHelpers.ToleranciaPes * 2;

            var outrasParedes = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.Id != paredeSelecionada.Id);

            foreach (Wall outra in outrasParedes)
            {
                LocationCurve outraCurva = outra.Location as LocationCurve;
                if (outraCurva == null || !(outraCurva.Curve is Line outraLinha))
                    continue;

                IntersectionResultArray resultadosIntersecao;
                SetComparisonResult comparacao = minhaLinha.Intersect(outraLinha, out resultadosIntersecao);
                if (comparacao != SetComparisonResult.Overlap || resultadosIntersecao == null)
                    continue;

                foreach (IntersectionResult ir in resultadosIntersecao)
                {
                    XYZ pontoCruz = ir.XYZPoint;

                    double meuParametro = minhaLinha.Project(pontoCruz).Parameter;
                    double outroParametro = outraLinha.Project(pontoCruz).Parameter;
                    double outroParamInicio = outraLinha.GetEndParameter(0);
                    double outroParamFim = outraLinha.GetEndParameter(1);

                    bool longeDasMinhasPontas = meuParametro > meuParamInicio + margemPontas
                        && meuParametro < meuParamFim - margemPontas;
                    bool longeDasPontasDaOutra = outroParametro > outroParamInicio + margemPontas
                        && outroParametro < outroParamFim - margemPontas;

                    if (!longeDasMinhasPontas || !longeDasPontasDaOutra)
                        continue;

                    XYZ outraDirecao = (outraLinha.GetEndPoint(1) - outraLinha.GetEndPoint(0)).Normalize();

                    resultado.Add(new CruzJunctionInfo
                    {
                        PontoCruz = pontoCruz,
                        ParedeCruzadaId = outra.Id,
                        DirecaoPropria = minhaDirecao,
                        DirecaoCruzada = outraDirecao,
                        EspessuraParedeCruzadaInterna = WallGeometryHelpers.ObterEspessuraNucleo(outra),
                        EspessuraParedePropriaInterna = WallGeometryHelpers.ObterEspessuraNucleo(paredeSelecionada)
                    });
                }
            }

            return resultado;
        }

        public static string GerarNomeQuadruplo(XYZ pontoCruz)
        {
            return $"{PrefixoQuadruplo}-{pontoCruz.X:F3}-{pontoCruz.Y:F3}-{pontoCruz.Z:F3}";
        }

        public static bool ExisteQuadruploEm(Document doc, XYZ pontoCruz)
        {
            string nomeEsperado = GerarNomeQuadruplo(pontoCruz);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>()
                .Any(ds => ds.Name == nomeEsperado);
        }

        public static List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm)> CalcularPontosQuadruplo(
            CruzJunctionInfo encontroCruz, double larguraPerfilCm = 4)
        {
            double espessuraCruzadaCm = RevitVersionAdapter.InternoParaCentimetros(
                encontroCruz.EspessuraParedeCruzadaInterna);

            double offset1Cm = (espessuraCruzadaCm / 2.0) - (larguraPerfilCm / 2.0);
            double offset2Cm = (espessuraCruzadaCm / 2.0) + (larguraPerfilCm / 2.0);
            double offset1Interno = RevitVersionAdapter.CentimetrosParaInterno(offset1Cm);
            double offset2Interno = RevitVersionAdapter.CentimetrosParaInterno(offset2Cm);

            XYZ direcaoOposta = encontroCruz.DirecaoPropria.Negate();

            XYZ pontoLado1_1 = encontroCruz.PontoCruz + encontroCruz.DirecaoPropria.Multiply(offset1Interno);
            XYZ pontoLado1_2 = encontroCruz.PontoCruz + encontroCruz.DirecaoPropria.Multiply(offset2Interno);
            XYZ pontoLado2_1 = encontroCruz.PontoCruz + direcaoOposta.Multiply(offset1Interno);
            XYZ pontoLado2_2 = encontroCruz.PontoCruz + direcaoOposta.Multiply(offset2Interno);

            return new List<(XYZ, XYZ, double)>
            {
                (pontoLado1_1, encontroCruz.DirecaoPropria, 90.0),
                (pontoLado1_2, encontroCruz.DirecaoPropria, 90.0),
                (pontoLado2_1, direcaoOposta, 90.0),
                (pontoLado2_2, direcaoOposta, 90.0)
            };
        }

        public static List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm)> CalcularPontosQuadruploCruzada(
            CruzJunctionInfo encontroCruz)
        {
            double offsetInterno = RevitVersionAdapter.CentimetrosParaInterno(6.5);
            XYZ direcaoOposta = encontroCruz.DirecaoPropria.Negate();

            XYZ ponto1 = encontroCruz.PontoCruz + encontroCruz.DirecaoPropria.Multiply(offsetInterno);
            XYZ ponto2 = encontroCruz.PontoCruz + direcaoOposta.Multiply(offsetInterno);

            return new List<(XYZ, XYZ, double)>
            {
                (ponto1, encontroCruz.DirecaoPropria, 90.0),
                (ponto2, direcaoOposta, 90.0)
            };
        }
    }
}
