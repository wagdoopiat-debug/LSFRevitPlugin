using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Constroi a geometria solida 3D de um montante. Quando a espessura da chapa e informada,
    /// desenha a secao real em "C" (alma + duas abas + labios); sem espessura, cai na caixa
    /// retangular simplificada de antes.
    /// </summary>
    public static class StudSolidBuilder
    {
        /// <summary>
        /// Para onde a abertura do "C" aponta: true = para a frente da direcao do montante
        /// (a alma fica do lado de tras); false = o contrario. Se todos os montantes aparecerem
        /// virados do lado errado, e so inverter este valor.
        /// </summary>
        public static bool AbrirParaFrenteDaDirecao = true;

        /// <summary>
        /// Cria um solido de montante usando as dimensoes do perfil escolhido pelo usuario:
        /// largura ao longo da parede = aba do perfil; profundidade perpendicular a parede =
        /// largura da alma; espessura da chapa e labio tambem vem do perfil.
        /// </summary>
        /// <param name="perfil">Perfil escolhido na janela de opcoes (nao pode ser nulo).</param>
        /// <param name="materialId">Material (cor) aplicado ao solido; nulo = sem material.</param>
        public static Solid CriarSolidoMontante(
            XYZ pontoBase, XYZ direcaoParede, double alturaInterna, PerfilSpec perfil,
            bool ehExtremidade = false, double deslocamentoAdicionalInterno = 0,
            ElementId materialId = null)
        {
            if (perfil == null) throw new ArgumentNullException(nameof(perfil));

            return CriarSolidoMontante(
                pontoBase, direcaoParede, alturaInterna, ehExtremidade,
                larguraMm: perfil.AbaMm,
                profundidadeMm: perfil.LarguraAlmaMm,
                deslocamentoAdicionalInterno: deslocamentoAdicionalInterno,
                materialId: materialId,
                espessuraChapaMm: perfil.EspessuraChapaMm,
                labioMm: perfil.LabioMm);
        }

        /// <summary>
        /// Cria o solido de um montante, orientado de acordo com a direcao real da parede.
        /// </summary>
        /// <param name="pontoBase">Ponto de insercao (base do montante), em unidades internas do Revit.</param>
        /// <param name="direcaoParede">Vetor unitario da direcao ao longo da parede neste ponto.</param>
        /// <param name="alturaInterna">Altura do montante, ja em unidades internas do Revit (pes).</param>
        /// <param name="ehExtremidade">Se true, desloca a secao meia largura para dentro da parede.</param>
        /// <param name="larguraMm">Largura nominal da secao (ao longo da parede = aba), em milimetros.</param>
        /// <param name="profundidadeMm">Profundidade nominal da secao (perpendicular a parede = alma), em milimetros.</param>
        /// <param name="deslocamentoAdicionalInterno">Deslocamento extra, em unidades internas do Revit,
        /// somado ao deslocamento de extremidade -- usado para o montante isolado em um encontro,
        /// onde vale -(metade da espessura da parede conectada).</param>
        /// <param name="materialId">Material (cor) aplicado ao solido; nulo = sem material.</param>
        /// <param name="espessuraChapaMm">Espessura da chapa do perfil; 0 ou menos = desenha caixa simples.</param>
        /// <param name="labioMm">Comprimento do labio (enrijecedor) do "C"; 0 = "C" sem labio.</param>
        public static Solid CriarSolidoMontante(
            XYZ pontoBase, XYZ direcaoParede, double alturaInterna, bool ehExtremidade = false,
            double larguraMm = 40, double profundidadeMm = 90, double deslocamentoAdicionalInterno = 0,
            ElementId materialId = null, double espessuraChapaMm = 0, double labioMm = 0)
        {
            double larguraInterna = RevitVersionAdapter.MilimetrosParaInterno(larguraMm);
            double profundidadeInterna = RevitVersionAdapter.MilimetrosParaInterno(profundidadeMm);

            XYZ direcaoNormalizada = direcaoParede.Normalize();
            XYZ direcaoPerpendicular = XYZ.BasisZ.CrossProduct(direcaoNormalizada).Normalize();

            double halfW = larguraInterna / 2.0;
            double halfD = profundidadeInterna / 2.0;

            XYZ centro = pontoBase;
            if (ehExtremidade)
            {
                centro = pontoBase + direcaoNormalizada.Multiply(halfW + deslocamentoAdicionalInterno);
            }

            List<XYZ> pontos;
            if (espessuraChapaMm > 0)
            {
                double t = SecaoPerfil.EspessuraModeladaInterna(espessuraChapaMm);
                double labio = RevitVersionAdapter.MilimetrosParaInterno(Math.Max(0, labioMm));
                pontos = CriarContornoC(
                    centro, direcaoNormalizada, direcaoPerpendicular,
                    halfW, halfD, t, labio, AbrirParaFrenteDaDirecao);
            }
            else
            {
                XYZ offsetLargura = direcaoNormalizada.Multiply(halfW);
                XYZ offsetProfundidade = direcaoPerpendicular.Multiply(halfD);

                pontos = new List<XYZ>
                {
                    centro - offsetLargura - offsetProfundidade,
                    centro + offsetLargura - offsetProfundidade,
                    centro + offsetLargura + offsetProfundidade,
                    centro - offsetLargura + offsetProfundidade,
                };
            }

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };

            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);

            return GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, alturaInterna, opcoes);
        }

        /// <summary>
        /// Contorno do "C" no plano da planta. Coordenadas locais: u ao longo da direcao do
        /// montante, v perpendicular a parede. A alma (lado longo) corre ao longo de v; as
        /// abas correm ao longo de u; os labios voltam ao longo de v. Sem labio valido, o
        /// contorno vira um "U" simples.
        /// </summary>
        /// <param name="a">Meia largura (metade da aba), em unidades internas.</param>
        /// <param name="w">Meia profundidade (metade da alma), em unidades internas.</param>
        /// <param name="t">Espessura da chapa modelada, em unidades internas.</param>
        /// <param name="labio">Comprimento do labio, em unidades internas.</param>
        /// <param name="abertoParaFrente">true = abertura voltada para +u.</param>
        private static List<XYZ> CriarContornoC(
            XYZ centro, XYZ dir, XYZ perp, double a, double w, double t, double labio, bool abertoParaFrente)
        {
            List<(double U, double V)> local = SecaoMontante.ContornoC(a, w, t, labio, abertoParaFrente);
            return local.Select(p => centro + dir.Multiply(p.U) + perp.Multiply(p.V)).ToList();
        }
    
        /// <summary>
        /// Cria o solido de um membro horizontal (ex.: blocking). A secao do perfil, no plano
        /// perpendicular a direcao de extrusao, e desenhada em cima de <paramref name="pontoInicial"/>
        /// e o solido se estende de <paramref name="pontoInicial"/> ao longo de
        /// <paramref name="dirExtrusaoLong"/> por <paramref name="comprimentoExtrusaoLongInterna"/>.
        /// Para a alma e a aba, vale a mesma convencao do montante vertical e da guia:
        /// <paramref name="dirSecaoPerp"/> e a normal da parede e carrega a alma (profundidade),
        /// e o terceiro eixo (vertical) carrega a aba (largura).
        /// </summary>
        public static Solid CriarSolidoMontanteHorizontal(
            XYZ pontoInicial,
            XYZ dirExtrusaoLong,
            XYZ dirSecaoPerp,
            double comprimentoExtrusaoLongInterna,
            double larguraMm = 40,
            double profundidadeMm = 90,
            ElementId materialId = null,
            double espessuraChapaMm = 0,
            double labioMm = 0,
            double deslocamentoTransversalInterno = 0,
            bool? abertoParaFrente = null)
        {
            if (pontoInicial == null || dirExtrusaoLong == null || dirSecaoPerp == null) return null;
            if (comprimentoExtrusaoLongInterna <= 0.0) return null;

            XYZ dE = dirExtrusaoLong.Normalize();
            XYZ dS = dirSecaoPerp.Normalize();
            if (Math.Abs(dS.DotProduct(dE)) > 1e-6)
            {
                dS = (dS - dE.Multiply(dS.DotProduct(dE))).Normalize();
            }

            // Segundo eixo da secao: perpendicular, ao mesmo tempo, ao eixo de extrusao (dE) e
            // ao eixo recebido (dS). Para um membro horizontal, dS e a normal da parede e dV e a
            // vertical. A secao TEM de ficar num plano perpendicular a dE; se um dos eixos da
            // secao for o proprio dE, CreateExtrusionGeometry rejeita o solido.
            XYZ dV = dE.CrossProduct(dS);
            if (dV.GetLength() < 1e-9)
            {
                dV = XYZ.BasisZ.CrossProduct(dE);
                if (dV.GetLength() < 1e-9) dV = XYZ.BasisY;
            }
            dV = dV.Normalize();

            double larguraInterna = RevitVersionAdapter.MilimetrosParaInterno(larguraMm);
            double profundidadeInterna = RevitVersionAdapter.MilimetrosParaInterno(profundidadeMm);

            double halfW = larguraInterna / 2.0;
            double halfD = profundidadeInterna / 2.0;

            XYZ centro = pontoInicial;
            if (Math.Abs(deslocamentoTransversalInterno) > 1e-12)
            {
                centro = pontoInicial + dV.Multiply(deslocamentoTransversalInterno);
            }

            bool afrente = abertoParaFrente ?? AbrirParaFrenteDaDirecao;

            List<XYZ> pontos;
            if (espessuraChapaMm > 0)
            {
                double t = SecaoPerfil.EspessuraModeladaInterna(espessuraChapaMm);
                double labio = RevitVersionAdapter.MilimetrosParaInterno(Math.Max(0, labioMm));
                pontos = CriarContornoC(
                    centro,
                    dV,
                    dS,
                    halfW,
                    halfD,
                    t,
                    labio,
                    afrente);
            }
            else
            {
                XYZ offW = dV.Multiply(halfW);
                XYZ offD = dS.Multiply(halfD);
                pontos = new List<XYZ>
                {
                    centro - offW - offD,
                    centro + offW - offD,
                    centro + offW + offD,
                    centro - offW + offD,
                };
            }

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);

            return GeometryCreationUtilities.CreateExtrusionGeometry(loops, dE, comprimentoExtrusaoLongInterna, opcoes);
        }
    }
}
