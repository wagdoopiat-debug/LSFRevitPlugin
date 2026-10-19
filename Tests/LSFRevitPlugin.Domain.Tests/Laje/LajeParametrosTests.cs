using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeParametrosTests
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
        public void Parametros_validos_nao_geram_erros()
        {
            Assert.Empty(Valido().Validar());
        }

        [Fact]
        public void Contorno_nulo_gera_erro()
        {
            var p = Valido();
            p.Contorno = null;

            Assert.Contains(p.Validar(), e => e.Contains("Contorno nao informado"));
        }

        [Fact]
        public void Contorno_invalido_propaga_erros()
        {
            var p = Valido();
            p.Contorno = new ContornoLaje(new[] { new Ponto2DMm(0, 0), new Ponto2DMm(1000, 0) });

            Assert.Contains(p.Validar(), e => e.Contains("pelo menos 3 pontos"));
        }

        [Fact]
        public void Perfil_nulo_gera_erro()
        {
            var p = Valido();
            p.Perfil = null;

            Assert.Contains(p.Validar(), e => e.Contains("Perfil nao informado"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-400.0)]
        [InlineData(double.NaN)]
        public void Espacamento_invalido_gera_erro(double espacamentoMm)
        {
            var p = Valido();
            p.EspacamentoMm = espacamentoMm;

            Assert.Contains(p.Validar(), e => e.Contains("Espacamento deve ser maior que zero"));
        }

        [Fact]
        public void Espessura_de_telha_fora_do_catalogo_gera_erro_na_laje_umida()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Umida;
            p.EspessuraTelhaMm = 0.65;

            Assert.Contains(p.Validar(), e => e.Contains("Espessura de telha invalida"));
        }

        [Fact]
        public void Espessura_de_telha_e_ignorada_na_laje_seca()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Seca;
            p.EspessuraTelhaMm = 0.65;

            Assert.Empty(p.Validar());
        }

        [Fact]
        public void Tipo_de_viga_ainda_nao_implementado_gera_erro()
        {
            var p = Valido();
            p.TipoViga = (TipoVigaLaje)999;

            Assert.Contains(p.Validar(), e => e.Contains("Tipo de viga nao implementado"));
        }

        [Fact]
        public void Direcao_ou_cota_nao_finitas_geram_erro()
        {
            var direcao = Valido();
            direcao.DirecaoVigasGraus = double.NaN;
            Assert.Contains(direcao.Validar(), e => e.Contains("Direcao das vigas"));

            var cota = Valido();
            cota.CotaMm = double.PositiveInfinity;
            Assert.Contains(cota.Validar(), e => e.Contains("Cota deve ser"));
        }

        [Fact]
        public void Perfil_Drywall_nao_e_aceito_para_laje()
        {
            var p = Valido();
            p.Perfil = new PerfilSpec
            {
                Sistema = SistemaPerfil.Drywall,
                LarguraAlmaMm = 48.0,
                EspessuraChapaMm = 0.50
            };

            Assert.Contains(p.Validar(), e => e.Contains("Perfil invalido para laje"));
            Assert.False(CatalogoPerfisLaje.EhPerfilDeLaje(p.Perfil));
        }

        [Fact]
        public void Catalogo_de_perfis_de_laje_nao_contem_drywall()
        {
            foreach (PerfilSpec perfil in CatalogoPerfisLaje.ObterTodos())
            {
                Assert.Equal(SistemaPerfil.SteelFrame, perfil.Sistema);
                Assert.True(CatalogoPerfisLaje.EhPerfilDeLaje(perfil));
            }
        }

        [Fact]
        public void Bitola_0_80_continua_rejeitada_na_laje()
        {
            var p = Valido();
            p.Perfil = new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = 90.0,
                EspessuraChapaMm = 0.80
            };

            Assert.Contains(p.Validar(), e => e.Contains("Perfil invalido para laje"));
        }
    }
}
