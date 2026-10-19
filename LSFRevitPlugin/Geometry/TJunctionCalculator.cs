using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>Deteccao e calculo do reforco Triplo em encontros de parede em T (ponta-meio).</summary>
    public static class TJunctionCalculator
    {
        private const string PrefixoTriplo = "LSF-Triplo";

        public static List<TJunctionInfo> DetectarEncontrosEmT(Document doc, Wall paredeSelecionada)
        {
            var resultado = new List<TJunctionInfo>();

            LocationCurve minhaCurva = paredeSelecionada.Location as LocationCurve;
            if (minhaCurva == null || !(minhaCurva.Curve is Line minhaLinha))
                throw new InvalidOperationException("Parede selecionada nao possui eixo reto valido.");

            XYZ meuInicio = minhaLinha.GetEndPoint(0);
            XYZ meuFim = minhaLinha.GetEndPoint(1);
            XYZ minhaDirecao = (meuFim - meuInicio).Normalize();

            var outrasParedes = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.Id != paredeSelecionada.Id);

            foreach (Wall outra in outrasParedes)
            {
                LocationCurve outraCurva = outra.Location as LocationCurve;
                if (outraCurva == null || !(outraCurva.Curve is Line outraLinha))
                    continue;

                foreach (XYZ meuPonto in new[] { meuInicio, meuFim })
                {
                    IntersectionResult projecao = outraLinha.Project(meuPonto);
                    if (projecao == null)
                        continue;

                    double distanciaPerpendicular = meuPonto.DistanceTo(projecao.XYZPoint);
                    if (distanciaPerpendicular > WallGeometryHelpers.ToleranciaPes)
                        continue;

                    double parametro = projecao.Parameter;
                    double paramInicio = outraLinha.GetEndParameter(0);
                    double paramFim = outraLinha.GetEndParameter(1);

                    double margemPontas = WallGeometryHelpers.ToleranciaPes * 2;
                    if (parametro < paramInicio + margemPontas || parametro > paramFim - margemPontas)
                        continue;

                    XYZ outraDirecao = (outraLinha.GetEndPoint(1) - outraLinha.GetEndPoint(0)).Normalize();

                    resultado.Add(new TJunctionInfo
                    {
                        PontoT = projecao.XYZPoint,
                        ParedeAtravessadaId = outra.Id,
                        DirecaoPropria = meuPonto.IsAlmostEqualTo(meuInicio) ? minhaDirecao : minhaDirecao.Negate(),
                        DirecaoPassante = outraDirecao,
                        EspessuraParedePassanteInterna = WallGeometryHelpers.ObterEspessuraNucleo(outra)
                    });
                }
            }

            return resultado;
        }

        public static List<TJunctionInfo> DetectarEncontrosEmTComoPassante(Document doc, Wall paredeSelecionada)
        {
            var resultado = new List<TJunctionInfo>();

            var outrasParedes = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.Id != paredeSelecionada.Id);

            foreach (Wall outra in outrasParedes)
            {
                List<TJunctionInfo> encontrosDaOutra;
                try
                {
                    encontrosDaOutra = DetectarEncontrosEmT(doc, outra);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                resultado.AddRange(encontrosDaOutra.Where(e => e.ParedeAtravessadaId == paredeSelecionada.Id));
            }

            return resultado;
        }

        public static string GerarNomeTriplo(XYZ pontoT)
        {
            return $"{PrefixoTriplo}-{pontoT.X:F3}-{pontoT.Y:F3}-{pontoT.Z:F3}";
        }

        public static bool ExisteTriploEm(Document doc, XYZ pontoT)
        {
            string nomeEsperado = GerarNomeTriplo(pontoT);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>()
                .Any(ds => ds.Name == nomeEsperado);
        }

        public static List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm)> CalcularPontosTriplo(
            TJunctionInfo encontroT, double larguraPerfilCm = 4)
        {
            double offsetInterno = RevitVersionAdapter.CentimetrosParaInterno(6.5);

            XYZ ponto = encontroT.PontoT + encontroT.DirecaoPropria.Multiply(offsetInterno);

            return new List<(XYZ, XYZ, double)>
            {
                (ponto, encontroT.DirecaoPropria, 90.0)
            };
        }

        public static List<(XYZ Ponto, XYZ Direcao, double ProfundidadeMm)> CalcularPontosTPassante(
            TJunctionInfo encontroT)
        {
            double offsetRotacionadoInterno = RevitVersionAdapter.CentimetrosParaInterno(2.5);
            double offsetNormalInterno = RevitVersionAdapter.CentimetrosParaInterno(6.5);

            XYZ pontoRotacionado = encontroT.PontoT + encontroT.DirecaoPropria.Multiply(offsetRotacionadoInterno);
            XYZ pontoNormal1 = encontroT.PontoT + encontroT.DirecaoPassante.Multiply(offsetNormalInterno);
            XYZ pontoNormal2 = encontroT.PontoT + encontroT.DirecaoPassante.Negate().Multiply(offsetNormalInterno);

            return new List<(XYZ, XYZ, double)>
            {
                (pontoRotacionado, encontroT.DirecaoPropria, 90.0),
                (pontoNormal1, encontroT.DirecaoPassante, 90.0),
                (pontoNormal2, encontroT.DirecaoPassante, 90.0)
            };
        }
    }
}
