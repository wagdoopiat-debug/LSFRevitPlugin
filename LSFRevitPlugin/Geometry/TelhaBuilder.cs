using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D da telha trapezoidal TP40 da laje umida. Cada faixa (TelhamentoTp40) e o
    /// recorte do perfil ondulado varrido ao longo de v (transversal as vigas), ja cortado pelo
    /// contorno real da laje. As chapas (colunas) se justapoem a cada largura util. O material
    /// caracteriza a chapa metalica; o enchimento das ondas usa o mesmo construtor, com outro papel.
    /// </summary>
    public static class TelhaBuilder
    {
        /// <summary>Cria os solidos de todas as faixas (chapa ou enchimento), na cota da camada.</summary>
        public static List<Solid> CriarSolidosFaixas(
            IEnumerable<FaixaPerfil> faixas,
            OrientacaoVigas orientacao,
            double zBaseMm,
            ElementId materialId = null)
        {
            var solidos = new List<Solid>();
            if (faixas == null || orientacao == null) return solidos;

            foreach (FaixaPerfil faixa in faixas)
            {
                Solid solido = CriarSolidoDaFaixa(faixa, orientacao, zBaseMm, materialId);
                if (solido != null) solidos.Add(solido);
            }

            return solidos;
        }

        /// <summary>Solido de uma faixa: o quadrado A-B-C-D (em u, z) extrusado ao longo de v.</summary>
        public static Solid CriarSolidoDaFaixa(
            FaixaPerfil faixa,
            OrientacaoVigas orientacao,
            double zBaseMm,
            ElementId materialId = null)
        {
            if (faixa == null || orientacao == null) return null;

            double comprimentoInterno = RevitVersionAdapter.MilimetrosParaInterno(faixa.ComprimentoMm);
            if (comprimentoInterno <= 1e-9) return null;

            var vAxis = new XYZ(orientacao.VX, orientacao.VY, 0.0);

            var pontos = new List<XYZ>(4)
            {
                PontoMundo(orientacao, faixa.A.XMm, faixa.VInicioMm, faixa.A.YMm, zBaseMm),
                PontoMundo(orientacao, faixa.B.XMm, faixa.VInicioMm, faixa.B.YMm, zBaseMm),
                PontoMundo(orientacao, faixa.C.XMm, faixa.VInicioMm, faixa.C.YMm, zBaseMm),
                PontoMundo(orientacao, faixa.D.XMm, faixa.VInicioMm, faixa.D.YMm, zBaseMm)
            };

            pontos = RemoverConsecutivosIguais(pontos);
            if (pontos.Count < 3) return null;

            try
            {
                var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
                var opcoes = new SolidOptions(
                    materialId ?? ElementId.InvalidElementId,
                    ElementId.InvalidElementId);

                return GeometryCreationUtilities.CreateExtrusionGeometry(
                    loops, vAxis, comprimentoInterno, opcoes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ponto do mundo numa coordenada (u, z) do segmento, fixando a posicao v: o ponto A/B/C/D
        /// ja esta em (u, z) e a faixa corre ao longo de v; o v de ancoragem e o VInicio.
        /// </summary>
        private static XYZ PontoMundo(
            OrientacaoVigas orientacao, double uMm, double vMm, double zMm, double zBaseMm)
        {
            Ponto2DMm ponto = orientacao.ParaMundo(uMm, vMm);
            return new XYZ(
                RevitVersionAdapter.MilimetrosParaInterno(ponto.XMm),
                RevitVersionAdapter.MilimetrosParaInterno(ponto.YMm),
                RevitVersionAdapter.MilimetrosParaInterno(zBaseMm + zMm));
        }

        /// <summary>
        /// Remove vertices consecutivos coincidentes (e o fechamento igual ao primeiro), para o
        /// enchimento de rampa virar um triangulo valido em vez de um quadrilatero degenerado.
        /// </summary>
        private static List<XYZ> RemoverConsecutivosIguais(List<XYZ> pontos)
        {
            var limpos = new List<XYZ>(pontos.Count);
            foreach (XYZ p in pontos)
            {
                if (limpos.Count == 0 || !limpos[limpos.Count - 1].IsAlmostEqualTo(p, 1e-9))
                    limpos.Add(p);
            }

            if (limpos.Count > 1 && limpos[0].IsAlmostEqualTo(limpos[limpos.Count - 1], 1e-9))
                limpos.RemoveAt(limpos.Count - 1);

            return limpos;
        }
    }
}
