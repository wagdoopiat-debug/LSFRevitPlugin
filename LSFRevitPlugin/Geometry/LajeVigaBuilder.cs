using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D das vigas de perfil simples e das guias de borda da laje.
    ///
    /// Viga: usa a mesma secao "C" do montante, mas deitada com a ALMA VERTICAL. No builder
    /// horizontal (StudSolidBuilder.CriarSolidoMontanteHorizontal), o eixo de secao que carrega a
    /// alma e passado como XYZ.BasisZ, de modo que a alma fica em pe e a aba (largura) corre na
    /// horizontal, transversal ao eixo da viga.
    ///
    /// Guia de borda: e a secao "U" da guia (SecaoGuia.ContornoU, sem labios), tambem com a ALMA
    /// VERTICAL e as abas HORIZONTAIS. A secao pura e reutilizada; aqui so os eixos sao mapeados:
    /// o eixo V do contorno (largura da alma) vai para a vertical (Z) e o eixo Z do contorno
    /// (altura da aba) vai para a horizontal apontando para dentro da laje.
    /// </summary>
    public static class LajeVigaBuilder
    {
        /// <summary>
        /// Solido de uma viga de perfil simples, com alma vertical, do inicio ao fim do eixo.
        /// A secao e desenhada centrada em <paramref name="inicioEixo"/> (que ja deve estar no
        /// centro da alma, no Z correto) e se estende ate <paramref name="fimEixo"/>.
        /// </summary>
        public static Solid CriarSolidoViga(
            PerfilSpec perfil, XYZ inicioEixo, XYZ fimEixo, ElementId materialId = null)
        {
            if (perfil == null) return null;

            XYZ eixo = fimEixo - inicioEixo;
            double comprimento = eixo.GetLength();
            if (comprimento <= 1e-9) return null;

            return StudSolidBuilder.CriarSolidoMontanteHorizontal(
                pontoInicial: inicioEixo,
                dirExtrusaoLong: eixo.Normalize(),
                dirSecaoPerp: XYZ.BasisZ,
                comprimentoExtrusaoLongInterna: comprimento,
                larguraMm: perfil.AbaMm,
                profundidadeMm: perfil.LarguraAlmaMm,
                materialId: materialId,
                espessuraChapaMm: perfil.EspessuraChapaMm,
                labioMm: perfil.LabioMm);
        }

        /// <summary>
        /// Solidos de uma viga composta (ou simples) a partir do descritor puro ComposicaoViga.
        /// PerfilSimples devolve 1 solido; BackToBack devolve 2 "C" costas com costas; Caixao
        /// devolve 2 "C": FrenteAFrente com aberturas opostas (abas sobrepostas, largura = AbaMm)
        /// e Encaixados com aberturas voltadas uma para a outra (almas nas bordas, pontas das
        /// abas no centro, largura = 2*AbaMm).
        /// A lista pode vir vazia se degenerado.
        /// </summary>
        public static List<Solid> CriarSolidosViga(
            PerfilSpec perfil, TipoVigaLaje tipo, XYZ inicioEixo, XYZ fimEixo, ElementId materialId = null)
        {
            return CriarSolidosViga(
                perfil, tipo, inicioEixo, fimEixo, materialId, ComposicaoCaixaoTipo.FrenteAFrente);
        }

        /// <summary>Solidos de uma viga composta, escolhendo a configuracao do caixao.</summary>
        public static List<Solid> CriarSolidosViga(
            PerfilSpec perfil, TipoVigaLaje tipo, XYZ inicioEixo, XYZ fimEixo, ElementId materialId,
            ComposicaoCaixaoTipo caixao)
        {
            var solidos = new List<Solid>();
            if (perfil == null) return solidos;

            XYZ eixo = fimEixo - inicioEixo;
            double comprimento = eixo.GetLength();
            if (comprimento <= 1e-9) return solidos;

            foreach (SubPerfilViga sub in ComposicaoViga.SubPerfis(tipo, perfil, caixao))
            {
                double deslocInterno =
                    RevitVersionAdapter.MilimetrosParaInterno(sub.DeslocamentoTransversalMm);
                bool afrente = sub.Espelhado
                    ? !StudSolidBuilder.AbrirParaFrenteDaDirecao
                    : StudSolidBuilder.AbrirParaFrenteDaDirecao;

                Solid solido = StudSolidBuilder.CriarSolidoMontanteHorizontal(
                    pontoInicial: inicioEixo,
                    dirExtrusaoLong: eixo.Normalize(),
                    dirSecaoPerp: XYZ.BasisZ,
                    comprimentoExtrusaoLongInterna: comprimento,
                    larguraMm: perfil.AbaMm,
                    profundidadeMm: perfil.LarguraAlmaMm,
                    materialId: materialId,
                    espessuraChapaMm: perfil.EspessuraChapaMm,
                    labioMm: perfil.LabioMm,
                    deslocamentoTransversalInterno: deslocInterno,
                    abertoParaFrente: afrente);

                if (solido != null) solidos.Add(solido);
            }

            return solidos;
        }

        /// <summary>
        /// Solido de uma placa plana da cobertura (OSB/cimenticia): extrusao vertical do contorno
        /// da laje, da base ao topo da camada. <paramref name="baseMm"/> e a cota Z absoluta (mm)
        /// da face inferior e <paramref name="espessuraMm"/> a altura da placa.
        /// </summary>
        public static Solid CriarSolidoPlaca(
            ContornoLaje contorno, double baseMm, double espessuraMm, ElementId materialId = null)
        {
            if (contorno == null || contorno.Pontos.Count < 3) return null;
            return CriarSolidoPlaca(contorno.Pontos, baseMm, espessuraMm, materialId);
        }

        /// <summary>
        /// Solido de uma placa plana a partir de uma lista de pontos de mundo (mm). Usado pelo
        /// parcelamento das chapas, cujo contorno ja vem recortado.
        /// </summary>
        public static Solid CriarSolidoPlaca(
            IReadOnlyList<Ponto2DMm> pontosMundo, double baseMm, double espessuraMm, ElementId materialId = null)
        {
            if (pontosMundo == null || pontosMundo.Count < 3) return null;

            double alturaInterna = RevitVersionAdapter.MilimetrosParaInterno(espessuraMm);
            if (alturaInterna <= 1e-9) return null;

            double zInterno = RevitVersionAdapter.MilimetrosParaInterno(baseMm);
            var pontos = new List<XYZ>(pontosMundo.Count);
            foreach (Ponto2DMm p in pontosMundo)
            {
                pontos.Add(new XYZ(
                    RevitVersionAdapter.MilimetrosParaInterno(p.XMm),
                    RevitVersionAdapter.MilimetrosParaInterno(p.YMm),
                    zInterno));
            }

            try
            {
                var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
                var opcoes = new SolidOptions(
                    materialId ?? ElementId.InvalidElementId,
                    ElementId.InvalidElementId);

                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, alturaInterna, opcoes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Solido de uma guia de borda (secao "U"), com alma vertical e abas horizontais voltadas
        /// para dentro da laje. <paramref name="paraDentro"/> e a direcao horizontal (unidades do
        /// Revit, nao precisa ser unitaria) para onde as abas crescem.
        /// </summary>
        public static Solid CriarSolidoGuiaBorda(
            PerfilSpec perfil, XYZ inicioEixo, XYZ fimEixo, XYZ paraDentro, ElementId materialId = null)
        {
            if (perfil == null || paraDentro == null) return null;

            XYZ eixo = fimEixo - inicioEixo;
            double comprimento = eixo.GetLength();
            if (comprimento <= 1e-9) return null;

            XYZ dE = eixo.Normalize();

            // Eixo da alma do "U" -> vertical; eixo da aba -> horizontal, perpendicular ao eixo da
            // guia e apontando para dentro. Se paraDentro coincidir com dE (degenerado), cai no
            // terceiro eixo para nao gerar um plano invalido.
            XYZ dAlma = XYZ.BasisZ;
            XYZ dAba = paraDentro - dE.Multiply(paraDentro.DotProduct(dE));
            if (dAba.GetLength() < 1e-9) dAba = dE.CrossProduct(dAlma);
            if (dAba.GetLength() < 1e-9) return null;
            dAba = dAba.Normalize();

            // Reutiliza a secao "U" pura: local (V, Z) -> mundo (dAlma * V + dAba * Z).
            List<(double V, double Z)> local = SecaoGuia.ContornoU(
                perfil.LarguraAlmaMm, perfil.AbaMm, perfil.EspessuraChapaMm, abertoParaCima: true);

            List<XYZ> pontos = local
                .Select(p => inicioEixo + dAlma.Multiply(p.V) + dAba.Multiply(p.Z))
                .ToList();

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);

            return GeometryCreationUtilities.CreateExtrusionGeometry(loops, dE, comprimento, opcoes);
        }
    }
}
