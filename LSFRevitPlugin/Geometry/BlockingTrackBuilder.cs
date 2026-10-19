using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>Papel construtivo de um trecho do blocking (guia cortada e dobrada).</summary>
    public enum PapelBlockingTrecho
    {
        /// <summary>Alma (web) horizontal do CORPO da guia, entre as duas linhas de dobra.</summary>
        Alma,

        /// <summary>Aba (flange) horizontal do CORPO, ao longo do corpo -- superficie de fixacao.</summary>
        AbaReta,

        /// <summary>
        /// Perna vertical da DOBRA: a ALMA da guia dobrada 90 graus PARA BAIXO, encostada ao lado
        /// da linha de dobra e descendo com profundidade <see cref="BlockingTrackBuilder.ProfundidadePernaBlockingMm"/>.
        /// </summary>
        PernaAlma,

        /// <summary>
        /// Perna vertical da DOBRA: as ABAS da guia dobradas 90 graus PARA BAIXO junto com a alma
        /// (a SECAO INTEIRA dobra junto), ao lado da linha de dobra, mesma descida da alma.
        /// </summary>
        PernaAba
    }

    /// <summary>
    /// Trecho do blocking descrito como caixa alinhada aos eixos LOCAIS da peca (funcao pura;
    /// sem depender do Revit). Eixos:
    /// U = ao longo do vao (do primeiro montante para o segundo);
    /// V = atraves da espessura da parede (0 = eixo da parede; +/- = faces);
    /// Z = vertical (0 = face inferior da alma).
    /// Todos os valores em unidades internas do Revit.
    /// </summary>
    public readonly struct TrechoBlocking
    {
        public PapelBlockingTrecho Papel { get; }
        public double U0 { get; }
        public double U1 { get; }
        public double V0 { get; }
        public double V1 { get; }
        public double Z0 { get; }
        public double Z1 { get; }

        public TrechoBlocking(PapelBlockingTrecho papel, double u0, double u1, double v0, double v1, double z0, double z1)
        {
            Papel = papel;
            U0 = u0; U1 = u1;
            V0 = v0; V1 = v1;
            Z0 = z0; Z1 = z1;
        }

        public double UExtensao => U1 - U0;
        public double VExtensao => V1 - V0;
        public double ZExtensao => Z1 - Z0;
    }

    /// <summary>
    /// Constroi o blocking horizontal como GUIA (track) cortada e dobrada, nao como montante.
    ///
    /// A peca usa a mesma secao "U" das guias (alma + 2 abas, SEM labios -- ver
    /// <see cref="SecaoGuia.ContornoU"/>), deitada com a alma na horizontal e as abas na vertical,
    /// e se estende entre os dois montantes adjacentes (mesma posicao e cota ja validadas por
    /// <see cref="BlockingCalculator"/>).
    ///
    /// Extremidades (dobra da SECAO INTEIRA, descricao confirmada pelo usuario):
    /// - ao encontrar cada montante, a SECAO INTEIRA da guia -- ALMA E AS DUAS ABAS JUNTAS -- e
    ///   cortada e dobrada 90 graus PARA BAIXO, formando uma perna vertical que abraca o montante;
    /// - a linha de dobra fica colada na face do montante: o CORPO horizontal (alma + abas, secao
    ///   "U" integra, sem cortes) vai de uma linha de dobra a outra, e cada perna e a CONTINUACAO
    ///   da propria secao, dobrada para baixo (a alma da guia vira placa vertical ao lado da dobra,
    ///   e as abas viram placas verticais ao lado dela -- a secao "U" se preserva na perna);
    /// - corpo e perna compartilham apenas a linha de dobra (encostam face a face, SEM lacuna nem
    ///   sobreposicao) -- a perna NAO e uma peca separada nem uma orelha extra.
    ///
    /// LIMITACOES EXPLICITAS: a linha/raio de dobra seria um vinco vivo (nenhum material na curva
    /// e modelado -- o canto entre alma e aba na regiao dobrada fica vazio, correspondente ao
    /// entalhe de alivio nao definido no projeto); a fixacao e a resistencia da ligacao NAO sao
    /// dimensionadas aqui. A profundidade da perna e uma constante provisoria desta classe
    /// (<see cref="ProfundidadePernaBlockingMm"/>). Ver tambem os avisos registrados por
    /// <see cref="Services.BlockingService"/>.
    /// </summary>
    public static class BlockingTrackBuilder
    {
        /// <summary>
        /// Profundidade (descida) da perna vertical formada pela dobra de 90 graus PARA BAIXO da
        /// secao inteira da guia em cada extremidade. Valor PROVISORIO (mm), a validar com
        /// engenharia e no Revit. A dobra so acontece quando o vao comporta as duas pernas
        /// (comprimento interno &gt;= 2x este valor); vao menor cai para a guia cheia, sem dobra.
        /// </summary>
        public const double ProfundidadePernaBlockingMm = 60.0;

        /// <summary>
        /// Secao transversal usada pelo blocking: exatamente a secao de guia/trilho em "U"
        /// (sem labios). Exposta para os testes comprovarem que o blocking NAO usa a secao de
        /// montante ("C" com labios).
        /// </summary>
        public static List<(double V, double Z)> ContornoDaSecao(PerfilSpec perfil)
        {
            return SecaoGuia.ContornoU(
                perfil.LarguraAlmaMm, perfil.AbaMm, perfil.EspessuraChapaMm, abertoParaCima: true);
        }

        /// <summary>
        /// Layout geometrico do blocking em coordenadas locais (funcao pura, testavel sem Revit).
        /// O CORPO em "U" (alma + 2 abas) e integro e vai de uma linha de dobra a outra, recuado
        /// de cada extremidade pela profundidade da perna; em cada extremidade a SECAO
        /// INTEIRA (alma + abas JUNTAS) e dobrada 90 graus PARA BAIXO na linha de dobra,
        /// formando uma perna vertical que abraca o montante
        /// (<see cref="PapelBlockingTrecho.PernaAlma"/> e <see cref="PapelBlockingTrecho.PernaAba"/>).
        /// A profundidade da perna e <see cref="ProfundidadePernaBlockingMm"/>.
        /// </summary>
        public static List<TrechoBlocking> GeometriaLocal(PerfilSpec perfil, double comprimentoInterno)
        {
            var trechos = new List<TrechoBlocking>();
            if (perfil == null || comprimentoInterno <= 0.0) return trechos;

            double t = SecaoPerfil.EspessuraModeladaInterna(perfil.EspessuraChapaMm);
            double meiaLargura = RevitVersionAdapter.MilimetrosParaInterno(perfil.LarguraAlmaMm) / 2.0 + t;
            double meioAlma = meiaLargura - t; // largura da alma / 2 = cota onde as flanges nascem
            double alturaAba = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);
            double d = RevitVersionAdapter.MilimetrosParaInterno(ProfundidadePernaBlockingMm);
            double L = comprimentoInterno;

            // A dobra de secao inteira exige vao com folga para as duas pernas (uma por extremidade);
            // sem isso cai para a guia cheia, sem dobra.
            bool temPerna = d > 0.0 && L >= 2.0 * d;

            double u0 = temPerna ? d : 0.0;
            double u1 = L - u0;

            // CORPO HORIZONTAL (U = u0 .. u1): ALMA + duas FLANGES, secao "U" integra de uma linha
            // de dobra a outra, SEM cortes -- nada aqui vira perna/orelha.
            if (u1 > u0)
            {
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.Alma, u0, u1, -meiaLargura, meiaLargura, 0.0, t));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.AbaReta, u0, u1, meioAlma, meiaLargura, t, alturaAba));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.AbaReta, u0, u1, -meiaLargura, -meioAlma, t, alturaAba));
            }

            if (temPerna)
            {
                // PERNA ESQUERDA: trecho U=[0,d] dobrado 90 graus PARA BAIXO na linha de dobra U=d.
                // A ALMA horizontal vira uma placa vertical (espessura em U = t) que margeia a linha
                // de dobra (U = d-t .. d) descendo Z = -d .. 0; as ABAS viram placas verticais
                // (U = d-aba .. d-t) no mesmo intervalo de descida. A boca da "U" fica voltada para
                // o montante (para fora do vao) e o perfil "U" da secao se preserva na perna.
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAlma, d - t, d, -meiaLargura, meiaLargura, -d, 0.0));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAba, d - alturaAba, d - t, meioAlma, meiaLargura, -d, 0.0));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAba, d - alturaAba, d - t, -meiaLargura, -meioAlma, -d, 0.0));

                // PERNA DIREITA: trecho U=[u1,L] dobrado 90 graus PARA BAIXO na linha de dobra U=u1,
                // imagem-espelho da perna esquerda (dobra para o lado de fora do vao).
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAlma, u1, u1 + t, -meiaLargura, meiaLargura, -d, 0.0));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAba, u1 + t, u1 + alturaAba, meioAlma, meiaLargura, -d, 0.0));
                trechos.Add(new TrechoBlocking(
                    PapelBlockingTrecho.PernaAba, u1 + t, u1 + alturaAba, -meiaLargura, -meioAlma, -d, 0.0));
            }

            return trechos;
        }

        /// <summary>
        /// Constroi os solidos do blocking (guia cortada e dobrada) no espaco absoluto do modelo.
        /// A alma fica centrada na cota Z de <paramref name="pontoInicial"/> (mesma convencao de
        /// posicionamento anterior). Devolve a lista de solidos (corpo horizontal + as duas pernas
        /// das extremidades: a SECAO INTEIRA da guia dobrada 90 graus para baixo abracando o
        /// montante).
        /// </summary>
        public static List<Solid> CriarSolidosTrackBlocking(
            XYZ pontoInicial, XYZ dirSpan, XYZ dirNormal, double comprimentoInterno,
            PerfilSpec perfil, ElementId materialId)
        {
            var solidos = new List<Solid>();
            if (pontoInicial == null || dirSpan == null || dirNormal == null || perfil == null)
                return solidos;
            if (comprimentoInterno <= 0.0) return solidos;

            XYZ dirU = dirSpan.Normalize();
            XYZ dirV = dirNormal.Normalize();
            XYZ dirZ = XYZ.BasisZ;

            double alturaAba = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);
            XYZ origem = new XYZ(pontoInicial.X, pontoInicial.Y, pontoInicial.Z - alturaAba / 2.0);

            foreach (var tr in GeometriaLocal(perfil, comprimentoInterno))
            {
                Solid s = CriarCaixa(
                    origem, dirU, dirV, dirZ,
                    tr.U0, tr.U1, tr.V0, tr.V1, tr.Z0, tr.Z1, materialId);
                if (s != null) solidos.Add(s);
            }

            return solidos;
        }

        /// <summary>
        /// Caixa retangular alinhada aos eixos locais: retangulo no plano (dirV, dirZ) na posicao
        /// U = u0, extrudado ao longo de dirU por (u1 - u0).
        /// </summary>
        private static Solid CriarCaixa(
            XYZ origem, XYZ dirU, XYZ dirV, XYZ dirZ,
            double u0, double u1, double v0, double v1, double z0, double z1, ElementId materialId)
        {
            double du = u1 - u0;
            double dv = v1 - v0;
            double dz = z1 - z0;
            if (du <= 0.0 || dv <= 0.0 || dz <= 0.0) return null;

            XYZ o = origem + dirU.Multiply(u0);
            XYZ p0 = o + dirV.Multiply(v0) + dirZ.Multiply(z0);
            XYZ p1 = o + dirV.Multiply(v1) + dirZ.Multiply(z0);
            XYZ p2 = o + dirV.Multiply(v1) + dirZ.Multiply(z1);
            XYZ p3 = o + dirV.Multiply(v0) + dirZ.Multiply(z1);

            var loop = SecaoPerfil.CriarLoop(new List<XYZ> { p0, p1, p2, p3 });
            return Extrudar(loop, dirU, du, materialId);
        }

        private static Solid Extrudar(CurveLoop loop, XYZ dir, double comprimento, ElementId materialId)
        {
            if (loop == null || comprimento <= 0.0) return null;
            var opcoes = new SolidOptions(
                materialId ?? ElementId.InvalidElementId,
                ElementId.InvalidElementId);
            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { loop }, dir.Normalize(), comprimento, opcoes);
        }
    }
}
