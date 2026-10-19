using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class BlockingCalculatorTests
    {
        private static PerfilSpec PerfilPadrao() => PerfilSpec.CriarPadrao();

        /// <summary>Profundidade (descida) provisoria da perna dobrada 90 graus para baixo.</summary>
        private static double Perna() =>
            RevitVersionAdapter.MilimetrosParaInterno(BlockingTrackBuilder.ProfundidadePernaBlockingMm);

        // Simula o que WallFraming emite numa parede de um painel so: o montante de COMECO de
        // painel (i==0) tem EhExtremidade=true / EhFimDoPainel=false; o de FIM (i==segmentos)
        // tem EhExtremidade=true / EhFimDoPainel=true; os internos sao regulares.
        private static List<(XYZ Ponto, bool EhExtremidade, bool EhFimDoPainel, bool EhPontaDaParede, int Painel)> PontosGradeSimples(
            XYZ inicio, XYZ dir, double espacamentoInterno, int segmentos)
        {
            var lista = new List<(XYZ,bool,bool,bool,int)>();
            for (int i = 0; i <= segmentos; i++)
            {
                double d = i * espacamentoInterno;
                XYZ p = inicio + dir.Multiply(d);
                bool ext = (i == 0 || i == segmentos);
                lista.Add((p, ext, i == segmentos, ext, 1));
            }
            return lista;
        }

        [Fact]
        public void ConfigValidar_LimitaValores()
        {
            var c = new BlockingConfig { Habilitado = true, Fileiras = 10 };
            c.Validar();
            Assert.Equal(5, c.Fileiras);
        }

        [Fact]
        public void ConfigValidar_NaoNegativo()
        {
            var c = new BlockingConfig { Habilitado = true, Fileiras = -2 };
            c.Validar();
            Assert.Equal(0, c.Fileiras);
        }

        [Fact]
        public void Calcular_Desabilitado_RetornaVazio()
        {
            var res = BlockingCalculator.Calcular(
                null, new XYZ(1,0,0), XYZ.Zero,
                new List<(XYZ,bool,bool,bool,int)>(),
                new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = false, Fileiras = 1 },
                PerfilPadrao(),
                p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0)); // not needed
            Assert.Empty(res);
        }

        [Fact]
        public void Calcular_ComprimentoEntreFaces_Espacamento40_Aba40mm()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            double aba40mmInt = RevitVersionAdapter.MilimetrosParaInterno(40.0);
            double meiaAbaInt = aba40mmInt * 0.5;
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 2);

            var res = BlockingCalculator.Calcular(
                null, dir, inicio,
                pts,
                new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                PerfilPadrao(),
                p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));
            Assert.Equal(2, res.Count);

            // Intervalos: [0(ext)->40(reg)] e [40(reg)->80(ext)].
            // Face de extremidade de comeco = a + aba; face regular = b - aba/2.
            // O blank tem o vao entre faces MAIS as duas profundidades de perna (dobra).
            double esperado = espac40cm - aba40mmInt - meiaAbaInt + 2.0 * Perna();
            foreach (var b in res)
            {
                Assert.True(Math.Abs(b.ComprimentoAoLongoEixo - esperado) < 1e-6);
                Assert.Equal(1, b.Painel);
            }
        }

        [Fact]
        public void Calcular_ComprimentoEntreFaces_Espacamento60_Aba35mm()
        {
            double espac60cm = RevitVersionAdapter.CentimetrosParaInterno(60.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac60cm, 1);

            var perfil = PerfilPadrao(); perfil.LarguraAlmaMm = 90; /* AbaMm/labio fixos por sistema; usar SteelFrame padrão */
            double abaInt = RevitVersionAdapter.MilimetrosParaInterno(perfil.AbaMm);

            var res = BlockingCalculator.Calcular(
                null, dir, inicio,
                pts,
                new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                perfil,
                p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));
            Assert.Single(res);

            // Unico intervalo entre os dois montantes de extremidade (inicio e fim do painel):
            // face a = ponto + aba; face b = ponto - aba, mais as duas profundidades de perna.
            double esperado = espac60cm - 2.0 * abaInt + 2.0 * Perna();
            Assert.True(Math.Abs(res[0].ComprimentoAoLongoEixo - esperado) < 1e-6);
        }

        [Fact]
        public void Calcular_QuantidadesAlturas_Fileiras()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 2);

            var res1 = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));
            Assert.Equal(2, res1.Count);

            var res3 = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 3 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));
            Assert.Equal(6, res3.Count);
        }

        [Fact]
        public void Calcular_ExcluiFileiraNaAlturaAbertura_PreservaDemais()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 1); // 1 intervalo

            double alturaUtil = RevitVersionAdapter.CentimetrosParaInterno(240.0);
            double folgaGuias = RevitVersionAdapter.CentimetrosParaInterno(OpeningReinforcement.FolgaEncaixeGuiaCm);
            double zBase = folgaGuias;
            double zTopo = alturaUtil - folgaGuias;
            double zMeio = zBase + (zTopo - zBase) * 0.5; // fileira central

            var vao = new OpeningInfo
            {
                PontoCentral = new XYZ(espac40cm * 0.5, 0, zMeio),
                LarguraInterna = espac40cm * 2.0,
                AlturaInterna = RevitVersionAdapter.CentimetrosParaInterno(80.0),
                AlturaSoleiraInterna = zMeio - RevitVersionAdapter.CentimetrosParaInterno(40.0)
            };

            var res3 = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo> { vao },
                new BlockingConfig { Habilitado = true, Fileiras = 3 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: alturaUtil);
            Assert.Equal(2, res3.Count);
            foreach (var b in res3)
            {
                Assert.True(b.AlturaZ < vao.AlturaSoleiraInterna - 1e-6 || b.AlturaZ > vao.AlturaSoleiraInterna + vao.AlturaInterna + 1e-6);
            }
        }

        [Fact]
        public void Calcular_LinhaDeDobra_EncostaNasFacesReaisDosMontantes()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            double abaInt = RevitVersionAdapter.MilimetrosParaInterno(PerfilPadrao().AbaMm);
            double meiaAbaInt = abaInt * 0.5;
            double d = Perna();
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 3); // 0e, 40r, 80r, 120e

            var res = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            Assert.Equal(3, res.Count);

            // [0(ext,inicio) -> 40(regular)]: face a em +aba, face b em -aba/2. O blank comeca uma
            // profundidade de perna antes da face a e termina uma profundidade depois da face b;
            // a LINHA DE DOBRA (= face) fica em PontoInicial + d e PontoFinal - d.
            double faceA0 = abaInt;
            double faceB0 = espac40cm - meiaAbaInt;
            Assert.True(Math.Abs(res[0].PontoInicial.X - (faceA0 - d)) < 1e-6);
            Assert.True(Math.Abs(res[0].PontoFinal.X - (faceB0 + d)) < 1e-6);
            Assert.True(Math.Abs(res[0].PontoInicial.X + d - faceA0) < 1e-6); // linha de dobra na face a
            Assert.True(Math.Abs(res[0].PontoFinal.X - d - faceB0) < 1e-6);   // linha de dobra na face b
            Assert.True(Math.Abs(res[0].ComprimentoAoLongoEixo - (espac40cm - abaInt - meiaAbaInt + 2 * d)) < 1e-6);

            // [40(regular) -> 80(regular)]: ambas as faces em +-aba/2.
            double faceA1 = espac40cm + meiaAbaInt;
            double faceB1 = 2 * espac40cm - meiaAbaInt;
            Assert.True(Math.Abs(res[1].PontoInicial.X - (faceA1 - d)) < 1e-6);
            Assert.True(Math.Abs(res[1].PontoFinal.X - (faceB1 + d)) < 1e-6);
            Assert.True(Math.Abs(res[1].ComprimentoAoLongoEixo - (espac40cm - abaInt + 2 * d)) < 1e-6);

            // [80(regular) -> 120(ext,fim)]: face a em +aba/2, face b em -aba.
            double faceA2 = 2 * espac40cm + meiaAbaInt;
            double faceB2 = 3 * espac40cm - abaInt;
            Assert.True(Math.Abs(res[2].PontoInicial.X - (faceA2 - d)) < 1e-6);
            Assert.True(Math.Abs(res[2].PontoFinal.X - (faceB2 + d)) < 1e-6);
            Assert.True(Math.Abs(res[2].ComprimentoAoLongoEixo - (espac40cm - meiaAbaInt - abaInt + 2 * d)) < 1e-6);

            // Comprimento sempre igual a distancia entre as extremidades (o blank).
            foreach (var b in res)
            {
                double comp = (b.PontoFinal - b.PontoInicial).DotProduct(dir);
                Assert.True(Math.Abs(comp - b.ComprimentoAoLongoEixo) < 1e-6);
            }
        }

        [Fact]
        public void Calcular_CotaAbsoluta_UsaElevacaoDaBaseDaParede()
        {
            // Base da parede a 3.0 de altura (ex.: piso acima do nivel 0). Tudo na parede
            // (montantes, guias, verga) soma essa elevacao; o blocking tem de fazer o mesmo.
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            double elevacaoBase = 3.0;
            XYZ inicio = new XYZ(0, 0, elevacaoBase);
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 1);

            double alturaUtil = RevitVersionAdapter.CentimetrosParaInterno(240.0);
            // Com uma fileira, tZ=0.5 => zf = (zBase + zTopo)/2 = alturaUtil/2.
            double zfEsperado = RevitVersionAdapter.CentimetrosParaInterno(120.0);
            double zAbsEsperado = elevacaoBase + zfEsperado;

            var res = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: alturaUtil);

            Assert.Single(res);
            var b = res[0];
            Assert.True(Math.Abs(b.AlturaZ - zAbsEsperado) < 1e-6,
                "AlturaZ deveria incluir a elevacao da base da parede (veio abaixo da parede).");
            Assert.True(b.AlturaZ > 1.0, "Blocking na cota ABSOLUTA, nao relativa.");
            Assert.True(Math.Abs(b.PontoInicial.Z - b.AlturaZ) < 1e-6);
            Assert.True(Math.Abs(b.PontoFinal.Z - b.AlturaZ) < 1e-6);
            Assert.True(Math.Abs(b.Centro.Z - b.AlturaZ) < 1e-6);
        }

        [Fact]
        public void Calcular_RegistraFinalidadeDaConfiguracao()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 1);

            var res = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Finalidade = FinalidadeBlocking.Apoio },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            Assert.Single(res);
            Assert.Equal(FinalidadeBlocking.Apoio, res[0].Finalidade);
        }

        [Fact]
        public void Calcular_UsaAlturasExplicitas_QuandoFileirasZero()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ inicio = XYZ.Zero;
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 1);

            double h100 = RevitVersionAdapter.CentimetrosParaInterno(100.0);
            double h200 = RevitVersionAdapter.CentimetrosParaInterno(200.0);

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
            Assert.Contains(res, b => Math.Abs(b.AlturaZ - h100) < 1e-6);
            Assert.Contains(res, b => Math.Abs(b.AlturaZ - h200) < 1e-6);
        }

        [Fact]
        public void Calcular_OrientacaoDiagonal_EndpointsAlinhadosAoEixoDaParede()
        {
            // Parede nao-cartesiana (diagonal): as extremidades do bloco tem de estar sobre a
            // linha da parede, encostadas nas faces dos montantes, e apontando ao longo dela.
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            double abaInt = RevitVersionAdapter.MilimetrosParaInterno(PerfilPadrao().AbaMm);
            double d = Perna();
            XYZ dir = new XYZ(0.6, -0.8, 0).Normalize();
            XYZ inicio = XYZ.Zero;
            var pts = PontosGradeSimples(inicio, dir, espac40cm, 1); // 2 extremidades

            var res = BlockingCalculator.Calcular(
                null, dir, inicio, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1 },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            Assert.Single(res);
            var b = res[0];

            // Mesma cota do bloco: a base da parede esta no nivel 0 e temos 1 fileira central.
            // O blank comeca uma profundidade de perna antes da face a, no eixo da parede.
            XYZ blankInicio = dir.Multiply(abaInt - d) + XYZ.BasisZ.Multiply(b.AlturaZ);
            XYZ blankFim = dir.Multiply(espac40cm - abaInt + d) + XYZ.BasisZ.Multiply(b.AlturaZ);
            Assert.True(b.PontoInicial.DistanceTo(blankInicio) < 1e-6);
            Assert.True(b.PontoFinal.DistanceTo(blankFim) < 1e-6);

            XYZ span = b.PontoFinal - b.PontoInicial;
            Assert.True(Math.Abs(span.CrossProduct(dir).GetLength()) < 1e-6,
                "Span do bloco paralelo ao eixo da parede (nao viesado).");
            Assert.True(Math.Abs(b.ComprimentoAoLongoEixo - span.GetLength()) < 1e-6);
        }

        // ------------------------------------------------------------------
        // Distribuicao: todos os vaos (padrao) x vaos alternados
        // ------------------------------------------------------------------

        [Fact]
        public void Calcular_DistribuicaoTodos_EhOPadrao()
        {
            var c = new BlockingConfig { Habilitado = true, Fileiras = 1 };
            c.Validar();
            Assert.Equal(DistribuicaoBlocking.Todos, c.Distribuicao);
            Assert.False(c.AlternarInvertido);
        }

        [Fact]
        public void Calcular_Alternado_GeraSomenteVaosAlternados()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(XYZ.Zero, dir, espac40cm, 3); // 0e,40r,80r,120e => 3 vaos

            var todos = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Todos },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));
            Assert.Equal(3, todos.Count);

            var alternado = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Alternado },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            // Vaos pares (paridades 0 e 2): sao exatamente os blocos todos[0] (0->40) e todos[2]
            // (80->120); o vao do meio (todos[1], 40->80) e omitido.
            Assert.Equal(2, alternado.Count);
            Assert.True(alternado[0].Centro.DistanceTo(todos[0].Centro) < 1e-6);
            Assert.True(alternado[1].Centro.DistanceTo(todos[2].Centro) < 1e-6);
        }

        [Fact]
        public void Calcular_Alternado_Invertido_DeslocaAParidade()
        {
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(XYZ.Zero, dir, espac40cm, 3); // 3 vaos: i=0,1,2

            var alternado = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Alternado },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            var invertido = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig
                {
                    Habilitado = true,
                    Fileiras = 1,
                    Distribuicao = DistribuicaoBlocking.Alternado,
                    AlternarInvertido = true
                },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            var todos = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Todos },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            // Normal pega i=0,2 (todos[0] e todos[2]); invertido pega somente i=1 => todos[1]
            // (vao 40->80).
            Assert.Equal(2, alternado.Count);
            Assert.Single(invertido);
            Assert.True(invertido[0].Centro.DistanceTo(todos[1].Centro) < 1e-6);
        }

        [Fact]
        public void Calcular_Alternado_MontantesNaoSeRepetem()
        {
            // Em vaos alternados (i=0 e i=2) os pares sao (0,1) e (2,3): nenhum montante e
            // compartilhado entre dois blocos (evita a duplicacao de pernas num mesmo montante).
            double espac40cm = RevitVersionAdapter.CentimetrosParaInterno(40.0);
            XYZ dir = XYZ.BasisX;
            var pts = PontosGradeSimples(XYZ.Zero, dir, espac40cm, 3);

            var todos = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Todos },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            var alternado = BlockingCalculator.Calcular(
                null, dir, XYZ.Zero, pts, new List<OpeningInfo>(),
                new BlockingConfig { Habilitado = true, Fileiras = 1, Distribuicao = DistribuicaoBlocking.Alternado },
                PerfilPadrao(), p => 1,
                alturaUtilInterna: RevitVersionAdapter.CentimetrosParaInterno(240.0));

            // O 1o bloco alternado cobre os montantes (0,1) = todos[0]; o 2o cobre (2,3) = todos[2].
            // O montante 1 (do 1o bloco) e o montante 2 (do 2o bloco) pertencem a blocos distintos,
            // sem compartilhar montante.
            Assert.Equal(2, alternado.Count);
            Assert.True(alternado[0].Centro.DistanceTo(todos[0].Centro) < 1e-6);
            Assert.True(alternado[1].Centro.DistanceTo(todos[2].Centro) < 1e-6);
            Assert.True(alternado[1].Centro.DistanceTo(alternado[0].Centro) > 1e-6);
        }
    }
}