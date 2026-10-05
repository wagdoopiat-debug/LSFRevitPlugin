using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class PaginadorAncoragemTests
    {
        private static readonly MedidaPlaca Medida = new MedidaPlaca(1200, 2400);

        private static EntradaPaginacao Entrada(double largura, bool ancorarNoFim)
        {
            // Montantes a cada 400 mm a partir do inicio e um montante na ponta final do painel.
            var montantes = new List<double>();
            for (double u = 0; u <= 2400; u += 400) montantes.Add(u);
            if (largura > 2400) montantes.Add(largura);

            return new EntradaPaginacao
            {
                NumeroPainel = 1,
                NumeroFace = 1,
                LarguraPainelMm = largura,
                AlturaMm = 2415,
                PosicoesMontantesMm = montantes,
                Tipo = TipoPlaca.GessoSt,
                Medida = Medida,
                Orientacao = OrientacaoPlaca.EmPe,
                AncorarNoFim = ancorarNoFim,

                // Estes testes foram escritos para a peca minima de 30 cm; fixam o valor para nao
                // depender do padrao.
                LarguraMinimaPecaMm = 300.0,
                DistanciaMinimaVerticeMm = 300.0
            };
        }

        [Fact]
        public void SemAncorarNoFim_APecaDaPontaFinalRecuaParaNaoFicarMenorQue30cm()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(2550, false), estoque);

            // A tira de 150 mm vira uma peca de 550 mm: a ultima junta recua para 2000.
            Assert.Equal(new List<double> { 1200, 2000 }, r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(550, r.Pecas.Last().Contorno.Largura, 3);
            Assert.All(r.Pecas, p => Assert.True(p.Contorno.Largura >= 300));
        }

        [Fact]
        public void AncorandoNoFim_ACantoFicaComPlacaGrande_EAPecaCortadaVaiParaOMeio()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(2550, true), estoque);

            // Placa de 1200 no inicio, peca cortada de 400 no meio e 950 na ponta final.
            Assert.Equal(new List<double> { 1200, 1600 }, r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(3, r.Pecas.Count);
            Assert.Equal(950, r.Pecas.Last().Contorno.Largura, 3);
            Assert.Equal(1200, r.Pecas.First().Contorno.Largura, 3);
            Assert.DoesNotContain(r.Avisos, a => a.Contains("fora de montante") || a.Contains("minimo"));
        }

        [Fact]
        public void AncorandoNoFim_ParedeQueJaFechaEmPlacasInteiras_NaoMuda()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(2400, true), estoque);

            Assert.Equal(new List<double> { 1200 }, r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(2, r.Pecas.Count);
        }
    }
}