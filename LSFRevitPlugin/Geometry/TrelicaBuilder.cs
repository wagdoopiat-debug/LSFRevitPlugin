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
            MontagemTrelica montagem = MontagemTrelica.Encaixada,
            double sinalNormalSecao = 1.0,
            SecaoBanzoTrelica secaoBanzo = SecaoBanzoTrelica.C,
            bool abrirMontantesParaDentro = false)
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
            return CriarSolidosMembros(
                perfil, membros, inicioEixo, uDir, normal, materialId, montagem,
                sinalNormalSecao, secaoBanzo, abrirMontantesParaDentro);
        }

        /// <summary>
        /// Solidos de uma lista de barras quaisquer (por exemplo, o conjunto de cinco pecas do
        /// travamento de apoio). As coordenadas u dos membros sao locais ao referencial
        /// (<paramref name="inicioEixo"/>, <paramref name="uDir"/>, normal, vertical).
        /// </summary>
        public static List<Solid> CriarSolidosMembros(
            PerfilSpec perfil,
            IReadOnlyList<MembroTrelica> membros,
            XYZ inicioEixo,
            XYZ uDir,
            XYZ normalPlano,
            ElementId materialId = null,
            MontagemTrelica montagem = MontagemTrelica.Encaixada,
            double sinalNormalSecao = 1.0,
            SecaoBanzoTrelica secaoBanzo = SecaoBanzoTrelica.C,
            bool abrirMontantesParaDentro = false)
        {
            var solidos = new List<Solid>();
            if (perfil == null || membros == null || inicioEixo == null || uDir == null) return solidos;
            if (uDir.GetLength() < 1e-9) return solidos;

            uDir = uDir.Normalize();

            XYZ normal = normalPlano;
            if (normal == null || normal.GetLength() < 1e-9)
                normal = XYZ.BasisZ.CrossProduct(uDir);
            if (normal.GetLength() < 1e-9) normal = XYZ.BasisY;
            normal = normal.Normalize();

            double uMin = double.PositiveInfinity;
            double uMax = double.NegativeInfinity;
            foreach (MembroTrelica m in membros)
            {
                if (m == null) continue;
                uMin = Math.Min(uMin, Math.Min(m.InicioUMm, m.FimUMm));
                uMax = Math.Max(uMax, Math.Max(m.InicioUMm, m.FimUMm));
            }

            double uCentro = double.IsInfinity(uMin) || double.IsInfinity(uMax)
                ? 0.0
                : (uMin + uMax) / 2.0;

            List<SubPerfilViga> subPerfis = ComposicaoViga.SubPerfisMontagem(montagem, perfil);

            var solidosBanzos = new List<Solid>();
            var solidosAlma = new List<Solid>();

            foreach (MembroTrelica membro in membros)
            {
                if (membro == null) continue;

                XYZ inicio = PontoLocal(inicioEixo, uDir, membro.InicioUMm, membro.InicioZMm);
                XYZ fim = PontoLocal(inicioEixo, uDir, membro.FimUMm, membro.FimZMm);

                bool banzo = membro.Tipo == MembroTrelicaTipo.BanzoSuperior
                    || membro.Tipo == MembroTrelicaTipo.BanzoInferior;
                bool montante = membro.Tipo == MembroTrelicaTipo.Montante;

                Vetor3D normalLocal = sinalNormalSecao < 0.0
                    ? new Vetor3D(0.0, -1.0, 0.0)
                    : SecaoMembroTrelica.NormalLocal;
                OrientacaoSecaoMembro orient =
                    SecaoMembroTrelica.Calcular(membro, montagem, normalLocal);
                XYZ dirSecaoPerp = ParaMundo(orient.DirecaoSecaoPerp, uDir, normal);
                XYZ dirExtrusao = banzo ? uDir : (fim - inicio).Normalize();
                double comprimentoInterno = RevitVersionAdapter.MilimetrosParaInterno(membro.ComprimentoMm);

                bool? aberturaParaDentro = null;
                if (abrirMontantesParaDentro && montante)
                {
                    // dV = dE x dS. Escolhe a abertura que aponta para o centro do conjunto.
                    XYZ dVMundo = dirExtrusao.CrossProduct(dirSecaoPerp);
                    double uMembro = (membro.InicioUMm + membro.FimUMm) / 2.0;
                    double sinal = dVMundo.DotProduct(uDir) * (uCentro - uMembro);
                    if (Math.Abs(sinal) > 1e-9) aberturaParaDentro = sinal > 0.0;
                }

                double labioMm = banzo
                    ? SecaoBanzoTrelicaRegra.LabioMm(secaoBanzo, perfil.LabioMm)
                    : perfil.LabioMm;

                foreach (SubPerfilViga sub in subPerfis)
                {
                    bool afrente = aberturaParaDentro
                        ?? (sub.Espelhado ? !orient.AbertoParaFrente : orient.AbertoParaFrente);

                    // A secao e modelada centrada no eixo, mas a alma do "C" fica a Aba/2 do
                    // centro. Em FaceComFace desloca-se o perfil para a alma real cair no plano
                    // da trelica (e nao o centro da secao), evitando que os corpos dos membros
                    // se interpenetrem nos nos. Encaixada mantem o perfil centrado.
                    double deslocMm = sub.DeslocamentoTransversalMm
                        + SecaoMembroTrelica.DeslocamentoAlmaMm(montagem, afrente, perfil.AbaMm);
                    double deslocInterno = RevitVersionAdapter.MilimetrosParaInterno(deslocMm);

                    Solid solido = StudSolidBuilder.CriarSolidoMontanteHorizontal(
                        pontoInicial: inicio,
                        dirExtrusaoLong: dirExtrusao,
                        dirSecaoPerp: dirSecaoPerp,
                        comprimentoExtrusaoLongInterna: comprimentoInterno,
                        larguraMm: perfil.AbaMm,
                        profundidadeMm: perfil.LarguraAlmaMm,
                        materialId: materialId,
                        espessuraChapaMm: perfil.EspessuraChapaMm,
                        labioMm: labioMm,
                        deslocamentoTransversalInterno: deslocInterno,
                        abertoParaFrente: afrente);

                    if (solido != null)
                    {
                        if (banzo)
                            solidosBanzos.Add(solido);
                        else
                            solidosAlma.Add(solido);
                    }
                }
            }

            solidos.AddRange(solidosBanzos);
            solidos.AddRange(solidosAlma);

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

