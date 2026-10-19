using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>Deteccao e calculo do reforco Duplo em encontros de parede em L (ponta-ponta).</summary>
    public static class LJunctionCalculator
    {
        private const string PrefixoReforcoCanto = "LSF-CantoH";

        public static List<WallJunctionInfo> DetectarEncontros(Document doc, Wall paredeSelecionada)
        {
            var resultado = new List<WallJunctionInfo>();

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

                XYZ outroInicio = outraLinha.GetEndPoint(0);
                XYZ outroFim = outraLinha.GetEndPoint(1);

                foreach (XYZ meuPonto in new[] { meuInicio, meuFim })
                {
                    XYZ pontoProximo = null;
                    if (meuPonto.DistanceTo(outroInicio) < WallGeometryHelpers.ToleranciaPes)
                        pontoProximo = outroInicio;
                    else if (meuPonto.DistanceTo(outroFim) < WallGeometryHelpers.ToleranciaPes)
                        pontoProximo = outroFim;

                    if (pontoProximo == null)
                        continue;

                    XYZ outraDirecao = (outroFim - outroInicio).Normalize();

                    double anguloRad = minhaDirecao.AngleTo(outraDirecao);
                    double anguloGraus = anguloRad * 180.0 / Math.PI;

                    resultado.Add(new WallJunctionInfo
                    {
                        PontoEncontro = meuPonto,
                        ParedeConectadaId = outra.Id,
                        AnguloGraus = anguloGraus,
                        DirecaoPropria = meuPonto.IsAlmostEqualTo(meuInicio) ? minhaDirecao : minhaDirecao.Negate(),
                        DirecaoConectada = pontoProximo.IsAlmostEqualTo(outroInicio) ? outraDirecao : outraDirecao.Negate(),
                        EspessuraParedeConectadaInterna = WallGeometryHelpers.ObterEspessuraNucleo(outra)
                    });
                }
            }

            return resultado;
        }

        public static string GerarNomeReforco(XYZ pontoEncontro)
        {
            return $"{PrefixoReforcoCanto}-{pontoEncontro.X:F3}-{pontoEncontro.Y:F3}-{pontoEncontro.Z:F3}";
        }

        public static bool ExisteReforcoCantoEm(Document doc, XYZ pontoEncontro)
        {
            string nomeEsperado = GerarNomeReforco(pontoEncontro);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>()
                .Any(ds => ds.Name == nomeEsperado);
        }

        public static List<(XYZ Ponto, XYZ Direcao)> CalcularPontosCantoCalifornia(
            WallJunctionInfo encontro, double larguraPerfilCm = 4)
        {
            double espessuraParedeConectadaCm = RevitVersionAdapter.InternoParaCentimetros(
                encontro.EspessuraParedeConectadaInterna);

            double offset1Cm = (espessuraParedeConectadaCm / 2.0) - (larguraPerfilCm / 2.0);
            double offset2Cm = (espessuraParedeConectadaCm / 2.0) + (larguraPerfilCm / 2.0);

            double offset1Interno = RevitVersionAdapter.CentimetrosParaInterno(offset1Cm);
            double offset2Interno = RevitVersionAdapter.CentimetrosParaInterno(offset2Cm);

            return new List<(XYZ, XYZ)>
            {
                (encontro.PontoEncontro + encontro.DirecaoPropria.Multiply(offset1Interno), encontro.DirecaoPropria),
                (encontro.PontoEncontro + encontro.DirecaoPropria.Multiply(offset2Interno), encontro.DirecaoPropria)
            };
        }
    }
}
