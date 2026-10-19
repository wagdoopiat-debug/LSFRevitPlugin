using System;
using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class PreparoEntradaPaginacaoTests
    {
        private static readonly MedidaPlaca Medida = new MedidaPlaca(1200, 2400);

        private static EntradaPaginacao Criar(
            double inicio, double fim, IEnumerable<double> montantes, IEnumerable<RetanguloMm> aberturas,
            int numeroFace = 1, double altura = 2700)
        {
            return PreparoEntradaPaginacao.Criar(
                7, numeroFace, inicio, fim, altura, montantes, aberturas,
                TipoPlaca.GessoSt, Medida, OrientacaoPlaca.EmPe);
        }

        [Fact]
        public void Montantes_ViramCoordenadasDoPainel_EOsDeForaSaoDescartados()
        {
            var entrada = Criar(
                1200, 2400,
                new List<double> { 20, 1180, 1220, 1600, 2000, 2380, 2420, 2500 },
                new List<RetanguloMm>());

            Assert.Equal(new List<double> { 20, 400, 800, 1180 }, entrada.PosicoesMontantesMm);
            Assert.Equal(1200, entrada.LarguraPainelMm, 3);
        }

        [Fact]
        public void MontantesQuaseNoMesmoLugar_ContamComoUmSo()
        {
            var entrada = Criar(0, 1200, new List<double> { 400, 400.4, 800 }, new List<RetanguloMm>());

            Assert.Equal(2, entrada.PosicoesMontantesMm.Count);
        }

        [Fact]
        public void AberturaQueAtravessaADivisa_EntraNosDoisPaineisComSuaParte()
        {
            var porta = new List<RetanguloMm> { new RetanguloMm(1000, 1500, 0, 2100) };

            var esquerdo = Criar(0, 1200, new List<double>(), porta);
            var direito = Criar(1200, 2400, new List<double>(), porta);

            RetanguloMm a = Assert.Single(esquerdo.Aberturas);
            Assert.Equal(1000, a.U0, 3);
            Assert.Equal(1200, a.U1, 3);

            RetanguloMm b = Assert.Single(direito.Aberturas);
            Assert.Equal(0, b.U0, 3);
            Assert.Equal(300, b.U1, 3);
            Assert.Equal(2100, b.Z1, 3);
        }

        [Fact]
        public void AberturaForaDoPainel_EDescartada()
        {
            var janela = new List<RetanguloMm> { new RetanguloMm(3000, 3500, 900, 2000) };

            var entrada = Criar(1200, 2400, new List<double>(), janela);

            Assert.Empty(entrada.Aberturas);
        }

        [Fact]
        public void AberturaMaisAltaQueAParede_ECortadaNaAltura()
        {
            var passagem = new List<RetanguloMm> { new RetanguloMm(500, 900, 0, 3000) };

            var entrada = Criar(0, 1200, new List<double>(), passagem, altura: 2700);

            Assert.Equal(2700, entrada.Aberturas[0].Z1, 3);
        }

        [Fact]
        public void Face2UsaDesencontroOposto_EOsDadosSaoRepassados()
        {
            var face1 = Criar(0, 2400, new List<double>(), new List<RetanguloMm>(), numeroFace: 1);
            var face2 = Criar(0, 2400, new List<double>(), new List<RetanguloMm>(), numeroFace: 2);

            Assert.Equal(0, face1.IndiceDeDesencontro);
            Assert.Equal(1, face2.IndiceDeDesencontro);
            Assert.Equal(7, face2.NumeroPainel);
            Assert.Equal(2, face2.NumeroFace);
            Assert.Equal(TipoPlaca.GessoSt, face2.Tipo);
            Assert.Equal(OrientacaoPlaca.EmPe, face2.Orientacao);
            Assert.True(face2.Medida.Equals(Medida));
        }

        [Fact]
        public void PainelInvalido_LancaExcecao()
        {
            Assert.Throws<ArgumentException>(
                () => Criar(1200, 1200, new List<double>(), new List<RetanguloMm>()));
        }

        [Fact]
        public void ComMontantesDeGrade400_AJuntaCaiNoMontanteDe1200()
        {
            // Montantes de extremidade a 20 mm das pontas e grade de 400 mm entre eles.
            var montantes = new List<double> { 20, 400, 800, 1200, 1600, 2000, 2380 };
            var entrada = Criar(0, 2400, montantes, new List<RetanguloMm>(), altura: 2400);
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);

            ResultadoPaginacao r = Paginador.Paginar(entrada, estoque);

            Assert.Equal(2, r.Pecas.Count);
            Assert.Single(r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(1200, r.JuntasVerticaisPorFiada[0][0], 3);
            Assert.DoesNotContain(r.Avisos, a => a.Contains("fora de montante"));
        }
    }
}