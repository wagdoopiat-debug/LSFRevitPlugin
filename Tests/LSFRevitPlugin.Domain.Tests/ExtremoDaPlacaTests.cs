using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class ExtremoDaPlacaTests
    {
        // Estrutura de 90 mm e placas de 10 mm nos dois lados do encontro.
        private const double Placa = 10.0;
        private const double EstruturaOutra = 90.0;

        [Fact]
        public void L_ParedeQueChega_Fim_FaceExternaRenteAEstrutura_FaceInternaNaFaceDoMontante()
        {
            // Parede de 3000; a estrutura passa 45 mm da ponta.
            Assert.Equal(3045, ExtremoDaPlaca.Calcular(3000, 3045, false, TipoEncontroNaPonta.EmL, false, Placa, EstruturaOutra), 3);
            Assert.Equal(2955, ExtremoDaPlaca.Calcular(3000, 3045, false, TipoEncontroNaPonta.EmL, true, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void L_ParedeQueRecebe_Fim_FaceExternaPassaALarguraDoOutroMontanteMaisAPlaca()
        {
            // A estrutura recua 45 mm da ponta: passa 90 + 10 alem do fim dela.
            Assert.Equal(3055, ExtremoDaPlaca.Calcular(3000, 2955, false, TipoEncontroNaPonta.EmL, false, Placa, EstruturaOutra), 3);

            // Face interna: para na placa da outra parede, 10 mm antes do fim da estrutura.
            Assert.Equal(2945, ExtremoDaPlaca.Calcular(3000, 2955, false, TipoEncontroNaPonta.EmL, true, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void L_PontaInicial_UsaOSentidoContrario()
        {
            // Chega: a estrutura comeca 45 mm antes do zero.
            Assert.Equal(-45, ExtremoDaPlaca.Calcular(3000, -45, true, TipoEncontroNaPonta.EmL, false, Placa, EstruturaOutra), 3);
            Assert.Equal(45, ExtremoDaPlaca.Calcular(3000, -45, true, TipoEncontroNaPonta.EmL, true, Placa, EstruturaOutra), 3);

            // Recebe: a estrutura comeca 45 mm depois do zero.
            Assert.Equal(-55, ExtremoDaPlaca.Calcular(3000, 45, true, TipoEncontroNaPonta.EmL, false, Placa, EstruturaOutra), 3);
            Assert.Equal(55, ExtremoDaPlaca.Calcular(3000, 45, true, TipoEncontroNaPonta.EmL, true, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void T_ParedeQueChega_EncostaNoPerfilDaParedeQuePassa()
        {
            // A placa vai ate a face da estrutura da outra: 45 mm antes do eixo (90 / 2).
            Assert.Equal(2955, ExtremoDaPlaca.Calcular(3000, 3000, false, TipoEncontroNaPonta.EmT, false, Placa, EstruturaOutra), 3);
            Assert.Equal(2955, ExtremoDaPlaca.Calcular(3000, 3000, false, TipoEncontroNaPonta.EmT, true, Placa, EstruturaOutra), 3);
            Assert.Equal(45, ExtremoDaPlaca.Calcular(3000, 0, true, TipoEncontroNaPonta.EmT, false, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void LarguraDoCorte_EncostaNoPerfil_OuFicaRecuadaPelaEspessuraDaPlaca()
        {
            // Encosta no perfil da outra: o corte e a espessura da estrutura dela.
            Assert.Equal(90, ExtremoDaPlaca.LarguraDoCorte(true, EstruturaOutra, Placa), 3);

            // Recuada: o corte inclui a placa da outra de cada lado (90 + 10 + 10).
            Assert.Equal(110, ExtremoDaPlaca.LarguraDoCorte(false, EstruturaOutra, Placa), 3);
        }

        [Fact]
        public void SemEncontro_APlacaVaiAteAPonta()
        {
            Assert.Equal(3000, ExtremoDaPlaca.Calcular(3000, 3045, false, TipoEncontroNaPonta.Nenhum, false, Placa, EstruturaOutra), 3);
            Assert.Equal(0, ExtremoDaPlaca.Calcular(3000, -45, true, TipoEncontroNaPonta.Nenhum, true, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void L_PontaLivre_EstruturaNaPonta_NaoMuda()
        {
            Assert.Equal(3000, ExtremoDaPlaca.Calcular(3000, 3002, false, TipoEncontroNaPonta.EmL, false, Placa, EstruturaOutra), 3);
            Assert.Equal(0, ExtremoDaPlaca.Calcular(3000, 3, true, TipoEncontroNaPonta.EmL, true, Placa, EstruturaOutra), 3);
        }

        [Fact]
        public void Interrupcao_ViraCoordenadaDoPainel_EAsDeForaSaoDescartadas()
        {
            var cortes = new List<RetanguloMm>
            {
                new RetanguloMm(1100, 1300, 0, 0),
                new RetanguloMm(5000, 5200, 0, 0)
            };

            List<RetanguloMm> noPainel = PreparoEntradaPaginacao.InterrupcoesNoPainel(cortes, 1200, 2400);

            RetanguloMm corte = Assert.Single(noPainel);
            Assert.Equal(0, corte.U0, 3);
            Assert.Equal(100, corte.U1, 3);
        }

        [Fact]
        public void PlacaNaoPassaPeloCorteDoCruzamento_EOsPedacosNaoTemRecorte()
        {
            var montantes = new List<double> { 0, 400, 800, 1200, 1600, 2000, 2400 };
            var cortes = new List<RetanguloMm> { new RetanguloMm(1100, 1300, 0, 0) };

            EntradaPaginacao entrada = PreparoEntradaPaginacao.Criar(
                1, 1, 0, 2400, 2400, montantes, new List<RetanguloMm>(),
                TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400), OrientacaoPlaca.EmPe, cortes);

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            ResultadoPaginacao r = Paginador.Paginar(entrada, estoque);

            Assert.Equal(4, r.Pecas.Count);
            Assert.All(r.Pecas, p => Assert.Empty(p.Recortes));

            foreach (PecaPlaca peca in r.Pecas)
            {
                foreach (RetanguloMm celula in peca.Celulas)
                {
                    Assert.False(celula.U1 > 1100.5 && celula.U0 < 1299.5, "peca " + peca.Codigo + " passa pelo corte");
                }
            }
        }
    }
}