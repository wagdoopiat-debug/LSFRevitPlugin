using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    /// <summary>
    /// Garante que o blocking horizontal e uma GUIA (track) em "U" -- nao um montante ("C" com
    /// labios) -- e que, ao encontrar os montantes, a SECAO INTEIRA da guia (ALMA E AS DUAS ABAS
    /// JUNTAS) e dobrada 90 graus PARA BAIXO, formando uma perna vertical que abraca o montante.
    /// O corpo horizontal (alma + abas, secao "U" integra) vai de uma linha de dobra a outra; cada
    /// perna e a continuacao da propria secao, dobrada para baixo ao lado da dobra
    /// (<see cref="PapelBlockingTrecho.PernaAlma"/> / <see cref="PapelBlockingTrecho.PernaAba"/>),
    /// compartilhando com o corpo apenas a linha de dobra (sem lacuna nem sobreposicao). NAO ha
    /// "abas de extremidade" (dobra de pontas de flanges para fora). Os testes de posicionamento
    /// puro ficam em <see cref="BlockingCalculatorTests"/>.
    /// </summary>
    public class BlockingTrackBuilderTests
    {
        private static PerfilSpec PerfilPadrao() => PerfilSpec.CriarPadrao();

        private static double Milimetros(double mm) => RevitVersionAdapter.MilimetrosParaInterno(mm);
        private static double T() => SecaoPerfil.EspessuraModeladaInterna(PerfilPadrao().EspessuraChapaMm);
        private static double MeiaLargura() =>
            RevitVersionAdapter.MilimetrosParaInterno(PerfilPadrao().LarguraAlmaMm) / 2.0 + T();
        private static double MeioAlma() => MeiaLargura() - T();
        private static double AlturaAba() =>
            RevitVersionAdapter.MilimetrosParaInterno(PerfilPadrao().AbaMm);

        /// <summary>Profundidade (descida) provisoria da perna dobrada 90 graus para baixo.</summary>
        private static double Perna() => Milimetros(BlockingTrackBuilder.ProfundidadePernaBlockingMm);

        /// <summary>Vao de teste com folga para as duas pernas (400 mm &gt;= 2x 60 mm).</summary>
        private static double VaoComPerna() => Milimetros(400.0);

        /// <summary>Dois trechos se sobrepoem em VOLUME (interiores com interseccao nao vazia).</summary>
        private static bool Sobrepoe(TrechoBlocking x, TrechoBlocking y)
        {
            const double eps = 1e-9;
            bool u = x.U0 + eps < y.U1 && y.U0 + eps < x.U1;
            bool v = x.V0 + eps < y.V1 && y.V0 + eps < x.V1;
            bool z = x.Z0 + eps < y.Z1 && y.Z0 + eps < x.Z1;
            return u && v && z;
        }

        [Fact]
        public void SecaoBlocking_EhGuiaU_SemLabios()
        {
            var contorno = BlockingTrackBuilder.ContornoDaSecao(PerfilPadrao());

            // "U" = 8 vertices; um "C" de montante tem 12 (dois labios).
            Assert.Equal(8, contorno.Count);
            Assert.Equal(3, contorno.Select(p => Math.Round(p.Z, 6)).Distinct().Count());
        }

        [Fact]
        public void SecaoBlocking_IgualASecaoDeGuia()
        {
            var perfil = PerfilPadrao();
            var guia = SecaoGuia.ContornoU(
                perfil.LarguraAlmaMm, perfil.AbaMm, perfil.EspessuraChapaMm, abertoParaCima: true);
            var blocking = BlockingTrackBuilder.ContornoDaSecao(perfil);

            Assert.True(guia.SequenceEqual(blocking),
                "O blocking tem de usar exatamente a mesma secao de guia/track em 'U'.");
        }

        [Fact]
        public void SecaoBlocking_Dimensoes_AlmaMaisDuasChapas()
        {
            var perfil = PerfilPadrao();
            var contorno = BlockingTrackBuilder.ContornoDaSecao(perfil);

            Assert.Equal(MeiaLargura(), contorno.Max(p => p.V), 6);
            Assert.Equal(-MeiaLargura(), contorno.Min(p => p.V), 6);
            Assert.Equal(AlturaAba(), contorno.Max(p => p.Z), 6);
            Assert.Equal(0.0, contorno.Min(p => p.Z), 6);
        }

        [Fact]
        public void Blocking_NaoUsaSecaoDeMontante()
        {
            var perfil = PerfilPadrao();
            double t = T();
            double halfW = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm) / 2.0;
            double halfD = RevitVersionAdapter.MilimetrosParaInterno(perfil.LarguraAlmaMm) / 2.0;
            double labio = RevitVersionAdapter.MilimetrosParaInterno(perfil.LabioMm);

            var secaoMontante = SecaoMontante.ContornoC(halfW, halfD, t, labio, abertoParaFrente: true);
            var secaoBlocking = BlockingTrackBuilder.ContornoDaSecao(perfil);

            Assert.Equal(12, secaoMontante.Count); // "C" com labios
            Assert.Equal(8, secaoBlocking.Count);  // "U" sem labios
            Assert.NotEqual(secaoMontante.Count, secaoBlocking.Count);
        }

        [Fact]
        public void Geometria_Dobra_SecaoInteira_ParaBaixo_EmCadaExtremidade()
        {
            double L = VaoComPerna();
            double d = Perna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // Os unicos papeis existentes sao os do corpo (Alma/AbaReta) e os da perna dobrada
            // para baixo (PernaAlma/PernaAba) -- nao ha mais "abas de extremidade".
            Assert.Equal(4, g.Select(x => x.Papel).Distinct().Count());
            Assert.Contains(g, x => x.Papel == PapelBlockingTrecho.Alma);
            Assert.Contains(g, x => x.Papel == PapelBlockingTrecho.AbaReta);
            Assert.Contains(g, x => x.Papel == PapelBlockingTrecho.PernaAlma);
            Assert.Contains(g, x => x.Papel == PapelBlockingTrecho.PernaAba);

            // A SECAO INTEIRA dobra junto nas duas extremidades: uma ALMA e as duas ABAS dobradas
            // (2 montantes x (1 alma + 2 abas)).
            var pernaAlma = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma).ToList();
            var pernaAba = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba).ToList();
            Assert.Equal(2, pernaAlma.Count);
            Assert.Equal(4, pernaAba.Count);

            // Linha de dobra de inicio em U=d; de fim em U=L-d.
            Assert.Single(pernaAlma, p => Math.Abs(p.U1 - d) < 1e-9);
            Assert.Single(pernaAlma, p => Math.Abs(p.U0 - (L - d)) < 1e-9);
        }

        [Fact]
        public void Geometria_AlmaEAba_DobramJuntas_NaMesmaLinhaDeDobra()
        {
            double L = VaoComPerna();
            double d = Perna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // Perna de INICIO: a alma dobrada termina exatamente na linha de dobra (U=d) e cada
            // aba dobrada encosta nela pelo lado do montante (termina na face da alma em U=d-t).
            var esq = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U1 < d + 1e-6)
                       .Concat(g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba && x.U1 < d + 1e-6))
                       .ToList();
            Assert.Equal(3, esq.Count);
            Assert.Single(esq, x => x.Papel == PapelBlockingTrecho.PernaAlma && Math.Abs(x.U1 - d) < 1e-9);
            var abasEsq = esq.Where(x => x.Papel == PapelBlockingTrecho.PernaAba).ToList();
            Assert.Equal(2, abasEsq.Count);
            foreach (var a in abasEsq)
                Assert.True(Math.Abs(a.U1 - (d - T())) < 1e-9,
                    "a aba dobrada encosta na alma dobrada (U = d - espessura).");

            // Perna de FIM: espelho -- alma comeca na linha de dobra (U=L-d) e abas ao lado.
            double u1 = L - d;
            var dir = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U0 > L / 2.0)
                       .Concat(g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba && x.U0 > L / 2.0))
                       .ToList();
            Assert.Equal(3, dir.Count);
            Assert.Single(dir, x => x.Papel == PapelBlockingTrecho.PernaAlma && Math.Abs(x.U0 - u1) < 1e-9);
            foreach (var a in dir.Where(x => x.Papel == PapelBlockingTrecho.PernaAba))
                Assert.True(Math.Abs(a.U0 - (u1 + T())) < 1e-9);
        }

        [Fact]
        public void Geometria_PernaDesceAbaixoDaAlma_AbaixoDeZZero()
        {
            double L = VaoComPerna();
            double d = Perna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // "90 graus PARA BAIXO": toda perna fica ABAIXO da alma horizontal (Z &lt;= 0),
            // descendo exatamente a profundidade provisoria.
            var pernas = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma
                                   || x.Papel == PapelBlockingTrecho.PernaAba).ToList();
            Assert.Equal(6, pernas.Count);
            foreach (var p in pernas)
            {
                Assert.Equal(-d, p.Z0, 6);
                Assert.Equal(0.0, p.Z1, 6);
            }
        }

        [Fact]
        public void Geometria_AlmaDobrada_ViraPlacaVertical_EspessuraDaChapaEmU()
        {
            double L = VaoComPerna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // A alma da perna e uma placa VERTICAL: a espessura da chapa fica ao longo de U
            // (extensao em U = chapa) e ela desce a profundidade da perna.
            foreach (var a in g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma))
            {
                Assert.Equal(T(), a.UExtensao, 6);
                Assert.Equal(Perna(), a.ZExtensao, 6);
                Assert.Equal(-MeiaLargura(), a.V0, 6);
                Assert.Equal(MeiaLargura(), a.V1, 6);
            }
        }

        [Fact]
        public void Geometria_AbaDobrada_ViraPlacaVertical_NasDuasFaces()
        {
            double L = VaoComPerna();
            double aba = AlturaAba();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // As abas dobradas preservam a altura da flange no PERFIL (extensao em U = aba -
            // espessura) e caem com a profundidade da perna, nos dois lados da parede (+V e -V).
            var abas = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba).ToList();
            Assert.Equal(4, abas.Count);
            Assert.Equal(2, abas.Count(a => a.V1 > 0));
            Assert.Equal(2, abas.Count(a => a.V0 < 0));
            foreach (var a in abas)
            {
                Assert.Equal(aba - T(), a.UExtensao, 6);
                Assert.Equal(Perna(), a.ZExtensao, 6);
                // Face interna da aba na raiz da alma (meioAlma), face externa na meiaLargura.
                Assert.Equal(MeioAlma(), a.V1 > 0 ? a.V0 : -a.V1, 6);
                Assert.Equal(MeiaLargura(), a.V1 > 0 ? a.V1 : -a.V0, 6);
            }
        }

        [Fact]
        public void Geometria_BocaDaU_VoltadaParaOMontante()
        {
            double L = VaoComPerna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // Em cada extremidade a boca (abertura) da "U" aponta para o montante (para fora do
            // vao): a alma da perna fica do lado do corpo e as abas do lado do montante,
            // encostando na alma e estendendo-se em direcao ao montante.
            // Esquerda (montante em U<0): abas encostam na alma e avancam para U menor.
            var almaEsq = g.Single(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U1 < L / 2.0);
            foreach (var a in g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba && x.U0 < L / 2.0))
            {
                Assert.True(a.U1 <= almaEsq.U0 + 1e-9,
                    "aba esquerda encostada na alma dobrada (boca continua).");
                Assert.True(a.U0 < almaEsq.U0 - 1e-9,
                    "boca da perna esquerda voltada para o montante (abas antes da alma).");
            }

            // Direita (montante em U>L): abas encostam na alma e avancam para U maior.
            var almaDir = g.Single(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U0 > L / 2.0);
            foreach (var a in g.Where(x => x.Papel == PapelBlockingTrecho.PernaAba && x.U0 > L / 2.0))
            {
                Assert.True(a.U0 >= almaDir.U1 - 1e-9,
                    "aba direita encostada na alma dobrada (boca continua).");
                Assert.True(a.U1 > almaDir.U1 + 1e-9,
                    "boca da perna direita voltada para o montante (abas depois da alma).");
            }
        }

        [Fact]
        public void Geometria_CorpoHorizontal_Integro_EntreAsLinhasDeDobra()
        {
            double L = VaoComPerna();
            double d = Perna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // O CORPO (alma + abas) corre de U=d a U=L-d (de uma linha de dobra a outra),
            // com a secao "U" integra (nada e cortado no corpo).
            var alma = Assert.Single(g, x => x.Papel == PapelBlockingTrecho.Alma);
            Assert.Equal(d, alma.U0, 6);
            Assert.Equal(L - d, alma.U1, 6);
            Assert.Equal(-MeiaLargura(), alma.V0, 6);
            Assert.Equal(MeiaLargura(), alma.V1, 6);
            Assert.Equal(0.0, alma.Z0, 6);
            Assert.Equal(T(), alma.Z1, 6);

            var retas = g.Where(x => x.Papel == PapelBlockingTrecho.AbaReta).ToList();
            Assert.Equal(2, retas.Count);
            foreach (var r in retas)
            {
                Assert.Equal(d, r.U0, 6);
                Assert.Equal(L - d, r.U1, 6);
            }
        }

        [Fact]
        public void Geometria_ContinuidadeNaLinhaDeDobra_SemLacunaNemSobreposicao()
        {
            double L = VaoComPerna();
            double d = Perna();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            // A alma do corpo e a alma da perna compartilham a face na linha de dobra (U=d e U=L-d):
            // alma da perna comeca exatamente onde o corpo termina.
            var corpo = g.Single(x => x.Papel == PapelBlockingTrecho.Alma);
            var almaEsq = g.Single(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U1 < L / 2.0);
            var almaDir = g.Single(x => x.Papel == PapelBlockingTrecho.PernaAlma && x.U0 > L / 2.0);
            Assert.Equal(corpo.U0, almaEsq.U1, 6);
            Assert.Equal(corpo.U1, almaDir.U0, 6);

            // NENHUM par de trechos sobrepoe VOLUME (encaixam face a face na dobra).
            for (int i = 0; i < g.Count; i++)
                for (int j = i + 1; j < g.Count; j++)
                    Assert.False(Sobrepoe(g[i], g[j]), "dois trechos se sobrepoem em volume.");
        }

        [Fact]
        public void Geometria_NadaForaDoVao_NemAcimaDaFlange()
        {
            double L = VaoComPerna();
            double d = Perna();
            double aba = AlturaAba();
            double meia = MeiaLargura();
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            foreach (var tr in g)
            {
                // Nada flutua para fora do vao em U; em V nada sai da faixa da secao; em Z nada
                // sobe acima da flange (a perna desce ateh -d, o corpo fica em [0, aba]).
                Assert.True(tr.U0 >= -1e-9 && tr.U1 <= L + 1e-9, "trecho fora do vao em U.");
                Assert.True(tr.V0 >= -meia - 1e-9 && tr.V1 <= meia + 1e-9,
                    "trecho fora da faixa da secao em V.");
                Assert.True(tr.Z0 >= -d - 1e-9 && tr.Z1 <= aba + 1e-9,
                    "trecho fora da faixa vertical permitida (perna ate -profundidade, corpo ate a flange).");
            }
        }

        [Fact]
        public void Geometria_VaoCurtoDemais_CaiParaGuiaCheia_SemPernas()
        {
            // Vao curto demais para as duas pernas (L < 2x profundidade): cai para a guia "U"
            // cheia, corpo de ponta a ponta e sem pernas.
            double L = Milimetros(100.0);
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), L);

            Assert.DoesNotContain(g, x => x.Papel == PapelBlockingTrecho.PernaAlma);
            Assert.DoesNotContain(g, x => x.Papel == PapelBlockingTrecho.PernaAba);

            var alma = Assert.Single(g, x => x.Papel == PapelBlockingTrecho.Alma);
            Assert.Equal(0.0, alma.U0, 6);
            Assert.Equal(L, alma.U1, 6);

            var retas = g.Where(x => x.Papel == PapelBlockingTrecho.AbaReta).ToList();
            Assert.Equal(2, retas.Count);
            foreach (var r in retas)
            {
                Assert.Equal(0.0, r.U0, 6);
                Assert.Equal(L, r.U1, 6);
            }
        }

        [Fact]
        public void Blocking_UsaGuia_MantendoPosicao_Altura_E_MultiplasFileiras()
        {
            // Configuracao com duas alturas explicitas independentes: o blocking continua
            // posicionado entre as faces dos montantes, na cota da fileira, e a secao usada
            // continua sendo a de guia "U".
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = new List<(XYZ, bool, bool, bool, int)>
            {
                (inicio, true, false, true, 1),
                (inicio + dir.Multiply(espac40cm), true, true, true, 1),
            };

            var res = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig
                {
                    Habilitado = true,
                    Fileiras = 0,
                    AlturasRelativasCm = new List<double> { 100, 200 }
                },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            Assert.Equal(2, res.Count);

            double abaInt = AlturaAba();
            double d = Perna();
            foreach (var b in res)
            {
                // O blank comeca/termina uma profundidade de perna FORA das faces internas
                // (extremidade de comeco em +aba; fim em -aba); a linha de dobra cai na face.
                Assert.Equal(abaInt - d, b.PontoInicial.X, 6);
                Assert.Equal(espac40cm - abaInt + d, b.PontoFinal.X, 6);
                Assert.Equal(espac40cm - 2 * abaInt + 2 * d, b.ComprimentoAoLongoEixo, 6);
            }

            Assert.Contains(res, b => Math.Abs(b.AlturaZ - RevitVersionAdapter.CentimetrosParaInterno(100.0)) < 1e-6);
            Assert.Contains(res, b => Math.Abs(b.AlturaZ - RevitVersionAdapter.CentimetrosParaInterno(200.0)) < 1e-6);

            // A geometria da guia cobre o vao calculado: corpo entre as linhas de dobra e pernas
            // dobradas 90 graus para baixo nas duas extremidades.
            var g = BlockingTrackBuilder.GeometriaLocal(PerfilPadrao(), res[0].ComprimentoAoLongoEixo);

            double L = res[0].ComprimentoAoLongoEixo;
            var alma = Assert.Single(g, x => x.Papel == PapelBlockingTrecho.Alma);
            Assert.Equal(d, alma.U0, 6);
            Assert.Equal(L - d, alma.U1, 6);
            var pernaAlma = g.Where(x => x.Papel == PapelBlockingTrecho.PernaAlma).ToList();
            Assert.Equal(2, pernaAlma.Count);
            Assert.Contains(pernaAlma, p => Math.Abs(p.U1 - d) < 1e-9);
            Assert.Contains(pernaAlma, p => Math.Abs(p.U0 - (L - d)) < 1e-9);
        }
    }
}