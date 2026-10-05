using System.Collections.Generic;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class PaginadorAlocacaoTests
    {
        private static readonly MedidaPlaca Medida = new MedidaPlaca(1200, 2400);

        // Um painel de 600 mm: uma peca de 600 x 2400 e a outra metade da placa vira sobra.
        private static ResultadoPaginacao Desenhar()
        {
            return Paginador.PaginarSemEstoque(new EntradaPaginacao
            {
                NumeroPainel = 1,
                NumeroFace = 1,
                LarguraPainelMm = 600,
                AlturaMm = 2415,
                PosicoesMontantesMm = new List<double> { 0, 600 },
                Tipo = TipoPlaca.GessoSt,
                Medida = Medida,
                Orientacao = OrientacaoPlaca.EmPe
            });
        }

        [Fact]
        public void PaginarSemEstoque_NaoAtribuiPlacaDeOrigem()
        {
            ResultadoPaginacao r = Desenhar();

            PecaPlaca peca = Assert.Single(r.Pecas);
            Assert.Null(peca.PlacaOrigem);
            Assert.False(peca.Reaproveitada);
        }

        [Fact]
        public void Alocar_PecasDeFacesDiferentesCompartilhamAsSobras()
        {
            ResultadoPaginacao primeira = Desenhar();
            ResultadoPaginacao segunda = Desenhar();
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);

            Paginador.Alocar(new[] { primeira, segunda }, estoque);

            // Uma so placa serve as duas pecas: a segunda sai da sobra da primeira.
            Assert.Equal(1, estoque.PlacasUsadas);
            Assert.False(primeira.Pecas[0].Reaproveitada);
            Assert.True(segunda.Pecas[0].Reaproveitada);
            Assert.Equal(primeira.Pecas[0].PlacaOrigem, segunda.Pecas[0].PlacaOrigem);
        }
    }
}