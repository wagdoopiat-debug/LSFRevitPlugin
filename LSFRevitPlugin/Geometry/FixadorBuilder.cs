using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D simplificada dos fixadores (parafusos). O fixador e modelado como um prisma
    /// retangular alongado no eixo da haste, centrado na posicao informada e orientado por
    /// <paramref name="direcao"/>:
    ///  - fixadores de placa sao verticais (atravessam a placa e entram na viga);
    ///  - fixadores estruturais entram pela ponta da viga, ao longo do eixo do vao, ate o membro
    ///    de borda.
    ///
    /// Simplificado: um unico bloco. Detalhado: cabeca (bloco achatado) + haste (bloco fino).
    /// Oculto nao gera geometria (tratado antes, no dominio).
    ///
    /// Os tamanhos sao GEOMETRICOS DE PARTIDA (nao normativos): o projetista define o fixador.
    /// </summary>
    public static class FixadorBuilder
    {
        public const double LadoSimplificadoMm = 12.0;
        public const double AlturaSimplificadoMm = 30.0;

        public const double LadoCabecaMm = 14.0;
        public const double AlturaCabecaMm = 4.0;
        public const double LadoHasteMm = 5.0;
        public const double AlturaHasteMm = 30.0;

        public static List<Solid> CriarSolidosParafuso(
            XYZ centro, NivelDetalheFixadores nivel, ElementId materialId = null)
        {
            return CriarSolidosParafuso(centro, XYZ.BasisZ, nivel, materialId);
        }

        /// <summary>Solidos do fixador centrado em <paramref name="centro"/>, com a haste ao longo de
        /// <paramref name="direcao"/> (horizontalizada se degenerada).</summary>
        public static List<Solid> CriarSolidosParafuso(
            XYZ centro, XYZ direcao, NivelDetalheFixadores nivel, ElementId materialId = null)
        {
            var solidos = new List<Solid>();
            if (centro == null) return solidos;
            if (nivel == NivelDetalheFixadores.Oculto) return solidos;

            XYZ eixo = direcao;
            if (eixo == null || eixo.GetLength() < 1e-9) eixo = XYZ.BasisZ;
            eixo = eixo.Normalize();

            if (nivel == NivelDetalheFixadores.Detalhado)
            {
                double deslocCabeca = RevitVersionAdapter.MilimetrosParaInterno(
                    (AlturaHasteMm + AlturaCabecaMm) / 2.0);

                Solid haste = Caixa(centro, LadoHasteMm, AlturaHasteMm, eixo, materialId);
                Solid cabeca = Caixa(centro + eixo.Multiply(deslocCabeca), LadoCabecaMm, AlturaCabecaMm, eixo, materialId);
                if (haste != null) solidos.Add(haste);
                if (cabeca != null) solidos.Add(cabeca);
                return solidos;
            }

            Solid simplificado = Caixa(centro, LadoSimplificadoMm, AlturaSimplificadoMm, eixo, materialId);
            if (simplificado != null) solidos.Add(simplificado);
            return solidos;
        }

        private static Solid Caixa(XYZ centro, double ladoMm, double alturaMm, XYZ direcao, ElementId materialId)
        {
            double lado = RevitVersionAdapter.MilimetrosParaInterno(ladoMm);
            double altura = RevitVersionAdapter.MilimetrosParaInterno(alturaMm);
            if (lado <= 1e-9 || altura <= 1e-9) return null;

            XYZ referencia = Math.Abs(direcao.DotProduct(XYZ.BasisZ)) > 0.99 ? XYZ.BasisX : XYZ.BasisZ;
            XYZ e1 = direcao.CrossProduct(referencia);
            if (e1.GetLength() < 1e-9) e1 = direcao.CrossProduct(XYZ.BasisY);
            if (e1.GetLength() < 1e-9) return null;
            e1 = e1.Normalize();
            XYZ e2 = direcao.CrossProduct(e1).Normalize();

            double meia = lado / 2.0;
            XYZ baseCentral = centro - direcao.Multiply(altura / 2.0);
            var pontos = new List<XYZ>
            {
                baseCentral + e1.Multiply(-meia) + e2.Multiply(-meia),
                baseCentral + e1.Multiply(meia) + e2.Multiply(-meia),
                baseCentral + e1.Multiply(meia) + e2.Multiply(meia),
                baseCentral + e1.Multiply(-meia) + e2.Multiply(meia)
            };

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);

            try
            {
                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, direcao, altura, opcoes);
            }
            catch
            {
                return null;
            }
        }
    }
}
