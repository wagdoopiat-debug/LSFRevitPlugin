using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajePreviaTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        private static ParametrosLaje Valido()
        {
            return new ParametrosLaje { Contorno = Retangulo() };
        }

        [Fact]
        public void Previa_valida_traz_contagens_e_area()
        {
            PreviaLaje p = PreviaLaje.Calcular(Valido(), CatalogoLaje.CriarPadrao());

            Assert.True(p.EhValido);
            Assert.True(p.QuantidadeVigas > 0);
            Assert.True(p.SegmentosDeViga > 0);
            Assert.True(p.QuantidadeGuiasBorda > 0);
            Assert.Equal(24.0, p.AreaM2, 3);
            Assert.True(p.AlturaTotalMm > 0);
            Assert.True(p.DirecaoAutomatica);
            Assert.NotEmpty(p.Camadas);
        }

        [Fact]
        public void Previa_de_contorno_nulo_tem_erro()
        {
            var p = Valido();
            p.Contorno = null;

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.False(previa.EhValido);
            Assert.Contains(previa.Erros, e => e.Contains("Contorno nao informado"));
        }

        [Fact]
        public void Previa_manual_sem_angulo_tem_erro()
        {
            var p = Valido();
            p.ModoOrientacao = OrientacaoVigasModo.Manual;

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.False(previa.EhValido);
        }

        [Fact]
        public void Previa_paralela_aresta_valida_define_direcao()
        {
            var p = Valido();
            p.ModoOrientacao = OrientacaoVigasModo.ParalelaAresta;
            p.ArestaReferencia = 0; // aresta horizontal -> 0 graus

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.True(previa.EhValido);
            Assert.False(previa.DirecaoAutomatica);
            Assert.Equal(0.0, previa.DirecaoEfetivaGraus.GetValueOrDefault(), 6);
        }

        [Fact]
        public void Linhas_de_previa_valida_nao_comecam_com_erro()
        {
            PreviaLaje p = PreviaLaje.Calcular(Valido(), CatalogoLaje.CriarPadrao());

            Assert.All(p.Linhas(), l => Assert.False(l.StartsWith("Erro:")));
            Assert.Contains(p.Linhas(), l => l.Contains("Area da laje"));
            Assert.Contains(p.Linhas(), l => l.Contains("Direcao das vigas"));
        }

        [Fact]
        public void Previa_usa_provider_de_apoios_para_alinhar_vigas()
        {
            OrientacaoVigas? capturada = null;
            PreviaLaje sem = PreviaLaje.Calcular(Valido(), CatalogoLaje.CriarPadrao());
            PreviaLaje com = PreviaLaje.Calcular(
                Valido(), CatalogoLaje.CriarPadrao(),
                o => { capturada = o; return new[] { o.VMinMm + 1230.0 }; },
                50.0);

            Assert.NotNull(capturada);
            Assert.NotEqual(sem.MaiorVaoEntreVigasMm, com.MaiorVaoEntreVigasMm);
        }

        [Fact]
        public void Previa_avisa_quando_nao_ha_montante_correspondente()
        {
            PreviaLaje com = PreviaLaje.Calcular(
                Valido(), CatalogoLaje.CriarPadrao(),
                o => new[] { o.VMinMm + 1280.0 },
                50.0);

            Assert.Contains(com.Avisos, a => a.Contains("sem montante/apoio correspondente"));
        }

        [Fact]
        public void Previa_nula_nao_lanca()
        {
            PreviaLaje p = PreviaLaje.Calcular(null, null);

            Assert.False(p.EhValido);
            Assert.NotEmpty(p.Erros);
        }

        [Fact]
        public void Previa_umida_estima_chapas_de_telha()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Umida;

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.True(previa.ChapasTelha > 0);
            Assert.Contains(previa.Linhas(), l => l.Contains("Chapas de telha"));
        }

        [Fact]
        public void Previa_com_placas_parceladas_conta_chapas()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Seca;
            p.ComposicaoPlacas = ComposicaoTipo.OSB;

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.True(previa.EhValido);
            Assert.True(previa.ChapasPlacas > 0);
            Assert.Contains(previa.Linhas(), l => l.Contains("Placas parceladas"));
        }

        [Fact]
        public void Previa_com_composicao_sempre_parcela_sem_opcao()
        {
            // A paginacao ficou obrigatoria: mesmo sem o antigo "PaginarPlacas", a previa conta chapas.
            var p = Valido();
            p.Tipo = TipoLaje.Seca;
            p.ComposicaoPlacas = ComposicaoTipo.OSBMaisCimenticia;

            PreviaLaje previa = PreviaLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.True(previa.EhValido);
            Assert.True(previa.ChapasPlacas > 0);
        }

        [Fact]
        public void Contagem_de_chapas_de_telha_arredonda_para_cima()
        {
            TelhaTp40 telha = TelhaTp40.CriarPadrao();

            // 100 mm x 100 mm cabe em uma chapa.
            Assert.Equal(1, SecaoTelhaTp40.ContagemChapas(100.0, 100.0, telha));

            // Acima da largura util (980) e do comprimento maximo (12000): 2 x 2 = 4.
            Assert.Equal(8, SecaoTelhaTp40.ContagemChapas(1500.0, 13000.0, telha));
        }
    }
}
