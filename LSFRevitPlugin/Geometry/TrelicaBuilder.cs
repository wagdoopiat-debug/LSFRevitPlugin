using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D da viga trelicada (vigota trelicada) da laje, a partir do padrao puro
    /// ComposicaoTrelica.
    ///
    /// Banzos e tambem diagonais/montantes usam a MESMA secao "C" com aba enrijecida do perfil
    /// escolhido (PerfilSpec) -- nao sao barras genericas. A orientacao da secao de cada peca vem
    /// da regra pura <see cref="SecaoMembroTrelica"/> (montagem + tipo do membro), garantindo que
    /// geometria e testes usem a mesma decisao: membros de alma ficam com a alma no plano da
    /// trelica e o BackToBack empilha os "C" para fora do plano; banzos Encaixada viram canal
    /// (guia) com a abertura para o interior.
    ///
    /// Coordenadas: as barras vem do dominio em (u, z) locais (u de 0 a vao, z em torno de zero);
    /// o mapeamento para o mundo usa <paramref name="inicioEixo"/> (ponta inicial no Z do centro da
    /// viga), a direcao unitaria do vao e a vertical do projeto.
    /// </summary>
    public static class TrelicaBuilder
    {
        /// <summary>
        /// Solidos das barras de UMA trelica. Devolve lista vazia se degenerada. <paramref name="vaoMm"/>
        /// deve corresponder a distancia entre <paramref name="inicioEixo"/> e <paramref name="fimEixo"/>.
        /// </summary>
        public static List<Solid> CriarSolidosTrelica(
            PerfilSpec perfil,
            TrelicaTipo tipo,
            double vaoMm,
            double alturaMm,
            int numeroPaineis,
            XYZ inicioEixo,
            XYZ fimEixo,
            XYZ normalPlano,
            ElementId materialId = null,
            MontagemTrelica montagem = MontagemTrelica.Encaixada)
        {
            var solidos = new List<Solid>();
            if (perfil == null) return solidos;

            XYZ eixo = fimEixo - inicioEixo;
            double comprimento = eixo.GetLength();
            if (comprimento <= 1e-9) return solidos;

            XYZ uDir = eixo.Normalize();

            XYZ normal = normalPlano;
            if (normal == null || normal.GetLength() < 1e-9)
                normal = XYZ.BasisZ.CrossProduct(uDir);
            if (normal.GetLength() < 1e-9) normal = XYZ.BasisY;
            normal = normal.Normalize();

            List<MembroTrelica> membros = ComposicaoTrelica.Membros(tipo, vaoMm, alturaMm, numeroPaineis);
            List<SubPerfilViga> subPerfis = ComposicaoViga.SubPerfisMontagem(montagem, perfil);

            foreach (MembroTrelica membro in membros)
            {
                XYZ inicio = PontoLocal(inicioEixo, uDir, membro.InicioUMm, membro.InicioZMm);
                XYZ fim = PontoLocal(inicioEixo, uDir, membro.FimUMm, membro.FimZMm);

                bool banzo = membro.Tipo == MembroTrelicaTipo.BanzoSuperior
                    || membro.Tipo == MembroTrelicaTipo.BanzoInferior;

                OrientacaoSecaoMembro orient = SecaoMembroTrelica.Calcular(membro, montagem);
                XYZ dirSecaoPerp = ParaMundo(orient.DirecaoSecaoPerp, uDir, normal);
                XYZ dirExtrusao = banzo ? uDir : (fim - inicio).Normalize();
                double comprimentoInterno = RevitVersionAdapter.MilimetrosParaInterno(membro.ComprimentoMm);

                foreach (SubPerfilViga sub in subPerfis)
                {
                    double deslocInterno =
                        RevitVersionAdapter.MilimetrosParaInterno(sub.DeslocamentoTransversalMm);
                    bool afrente = sub.Espelhado
                        ? !orient.AbertoParaFrente
                        : orient.AbertoParaFrente;

                    Solid solido = StudSolidBuilder.CriarSolidoMontanteHorizontal(
                        pontoInicial: inicio,
                        dirExtrusaoLong: dirExtrusao,
                        dirSecaoPerp: dirSecaoPerp,
                        comprimentoExtrusaoLongInterna: comprimentoInterno,
                        larguraMm: perfil.AbaMm,
                        profundidadeMm: perfil.LarguraAlmaMm,
                        materialId: materialId,
                        espessuraChapaMm: perfil.EspessuraChapaMm,
                        labioMm: perfil.LabioMm,
                        deslocamentoTransversalInterno: deslocInterno,
                        abertoParaFrente: afrente);

                    if (solido != null) solidos.Add(solido);
                }
            }

            return solidos;
        }

        /// <summary>
        /// Mapeia uma direcao local da trelica (x = u, y = normal, z = vertical) para o mundo.
        /// O referencial local (uDir, normal, vertical) e ortogonal direito, entao os produtos
        /// vetoriais (largura/abertura) sao preservados na conversao.
        /// </summary>
        private static XYZ ParaMundo(Vetor3D local, XYZ uDir, XYZ normal)
        {
            return uDir.Multiply(local.X)
                + normal.Multiply(local.Y)
                + XYZ.BasisZ.Multiply(local.Z);
        }

        private static XYZ PontoLocal(XYZ origem, XYZ uDir, double uMm, double zMm)
        {
            return origem
                + uDir.Multiply(RevitVersionAdapter.MilimetrosParaInterno(uMm))
                + XYZ.BasisZ.Multiply(RevitVersionAdapter.MilimetrosParaInterno(zMm));
        }
    }
}
