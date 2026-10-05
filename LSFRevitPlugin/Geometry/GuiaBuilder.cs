using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Constroi a geometria solida 3D das guias (trilhos horizontais), com a secao real em "U"
    /// (alma + duas abas, sem labios), onde os montantes se encaixam por dentro.
    /// </summary>
    public static class GuiaBuilder
    {
        /// <summary>
        /// Cria o solido de uma guia, estendida ao longo de um trecho do eixo da parede.
        /// </summary>
        /// <param name="inicioParede">Ponto inicial do trecho no eixo da parede, em unidades internas.</param>
        /// <param name="direcaoParede">Vetor unitario da direcao da parede.</param>
        /// <param name="comprimentoInterno">Comprimento do trecho, em unidades internas.</param>
        /// <param name="perfil">Perfil escolhido -- define alma, aba (altura das abas da guia) e chapa.</param>
        /// <param name="zBase">Cota Z da base da guia (unidades internas), relativa ao nivel
        /// da parede -- 0 para a guia inferior, ou (alturaParedeInterna - alturaGuiaInterna)
        /// para a guia superior.</param>
        /// <param name="materialId">Material (cor) aplicado ao solido; nulo = sem material.</param>
        /// <param name="abertoParaCima">true = alma embaixo e abas para cima (guia inferior,
        /// contra-verga); false = alma em cima e abas para baixo (guia superior, verga).</param>
        public static Solid CriarSolidoGuia(
            XYZ inicioParede, XYZ direcaoParede, double comprimentoInterno,
            PerfilSpec perfil, double zBase, ElementId materialId = null, bool abertoParaCima = true)
        {
            XYZ direcaoNormalizada = direcaoParede.Normalize();
            XYZ direcaoPerpendicular = XYZ.BasisZ.CrossProduct(direcaoNormalizada).Normalize();

            double t = SecaoPerfil.EspessuraModeladaInterna(perfil.EspessuraChapaMm);
            double almaMontanteInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.LarguraAlmaMm);
            double alturaAbaInterna = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);

            // Largura externa da guia = alma do montante + 2 chapas: o montante encaixa por dentro.
            double meiaLargura = (almaMontanteInterna / 2.0) + t;

            XYZ origem = new XYZ(inicioParede.X, inicioParede.Y, inicioParede.Z + zBase);

            // Contorno no plano perpendicular a parede: v ao longo de direcaoPerpendicular, z na vertical.
            // Desenhado "aberto para cima" (alma em z = 0..t).
            var local = new List<(double V, double Z)>
            {
                (-meiaLargura, 0),
                (meiaLargura, 0),
                (meiaLargura, alturaAbaInterna),
                (meiaLargura - t, alturaAbaInterna),
                (meiaLargura - t, t),
                (-meiaLargura + t, t),
                (-meiaLargura + t, alturaAbaInterna),
                (-meiaLargura, alturaAbaInterna),
            };

            if (!abertoParaCima)
            {
                // Espelha em z e inverte a ordem, para manter o mesmo sentido de percurso.
                local = local.Select(p => (p.V, alturaAbaInterna - p.Z)).Reverse().ToList();
            }

            List<XYZ> pontos = local
                .Select(p => origem + direcaoPerpendicular.Multiply(p.V) + XYZ.BasisZ.Multiply(p.Z))
                .ToList();

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };

            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);

            // A secao fica num plano perpendicular a parede e e extrudada ao longo dela.
            return GeometryCreationUtilities.CreateExtrusionGeometry(loops, direcaoNormalizada, comprimentoInterno, opcoes);
        }
    }
}