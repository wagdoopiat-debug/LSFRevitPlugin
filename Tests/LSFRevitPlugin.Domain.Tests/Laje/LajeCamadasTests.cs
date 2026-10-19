using System;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeCamadasTests
    {
        // ---------- Testes originais do Lote 2 (mantidos) ----------

        [Fact]
        public void Laje_umida_tem_argamassa_telha_e_viga_de_cima_para_baixo()
        {
            CamadasLaje c = CamadasLaje.CalcularUmida(3000.0, 90.0, 40.0, 50.0);

            Assert.Equal(TipoLaje.Umida, c.Tipo);
            Assert.Equal(3, c.Camadas.Count);

            CamadaLaje argamassa = c.Camada(CamadaLaje.NomeArgamassa);
            CamadaLaje telha = c.Camada(CamadaLaje.NomeTelha);
            CamadaLaje viga = c.Camada(CamadaLaje.NomeViga);

            Assert.Equal(3000.0, argamassa.TopoMm, 6);
            Assert.Equal(2950.0, argamassa.BaseMm, 6);
            Assert.Equal(2950.0, telha.TopoMm, 6);
            Assert.Equal(2910.0, telha.BaseMm, 6);
            Assert.Equal(2910.0, viga.TopoMm, 6);
            Assert.Equal(2820.0, viga.BaseMm, 6);

            Assert.Equal(3000.0, c.TopoConjuntoMm, 6);
            Assert.Equal(2910.0, c.TopoVigaMm, 6);
            Assert.Equal(2820.0, c.BaseVigaMm, 6);
            Assert.Equal(180.0, c.AlturaTotalMm, 6);
        }

        [Fact]
        public void Laje_seca_tem_piso_e_viga()
        {
            CamadasLaje c = CamadasLaje.CalcularSeca(3000.0, 90.0, 18.0);

            Assert.Equal(TipoLaje.Seca, c.Tipo);
            Assert.Equal(2, c.Camadas.Count);
            Assert.Equal(2982.0, c.Camada(CamadaLaje.NomePisoSeco).BaseMm, 6);
            Assert.Equal(2982.0, c.TopoVigaMm, 6);
            Assert.Equal(2892.0, c.BaseVigaMm, 6);
            Assert.Null(c.Camada(CamadaLaje.NomeTelha));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Soma_das_espessuras_e_a_diferenca_entre_topo_e_base(bool seca)
        {
            CamadasLaje c = seca
                ? CamadasLaje.CalcularSeca(3000.0, 140.0, 18.0)
                : CamadasLaje.CalcularUmida(3000.0, 140.0, 40.0, 50.0);

            double soma = 0.0;
            for (int i = 0; i < c.Camadas.Count; i++)
            {
                soma += c.Camadas[i].EspessuraMm;
                if (i > 0) Assert.Equal(c.Camadas[i - 1].BaseMm, c.Camadas[i].TopoMm, 9);
            }

            Assert.Equal(c.TopoConjuntoMm - c.BaseVigaMm, soma, 6);
        }

        [Fact]
        public void Valores_nao_positivos_ou_nao_finitos_sao_rejeitados()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularUmida(3000.0, 0.0, 40.0, 50.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularUmida(3000.0, 90.0, -1.0, 50.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularUmida(3000.0, 90.0, 40.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularSeca(3000.0, 90.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularSeca(3000.0, double.NaN, 18.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.CalcularUmida(double.PositiveInfinity, 90.0, 40.0, 50.0));
        }

        [Fact]
        public void Calcular_pelos_parametros_umida_usa_perfil_e_catalogo_padrao()
        {
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Umida, CotaMm = 3000.0 };

            CamadasLaje c = CamadasLaje.Calcular(parametros, CatalogoLaje.CriarPadrao());

            Assert.Equal(2910.0, c.TopoVigaMm, 6);
            Assert.Equal(2820.0, c.BaseVigaMm, 6);
        }

        [Fact]
        public void Calcular_pelos_parametros_seca_usa_o_piso_do_catalogo()
        {
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Seca, CotaMm = 3000.0 };

            CamadasLaje c = CamadasLaje.Calcular(parametros, CatalogoLaje.CriarPadrao());

            Assert.Equal(2982.0, c.TopoVigaMm, 6);
            Assert.Equal(2892.0, c.BaseVigaMm, 6);
        }

        [Fact]
        public void Alterar_o_catalogo_muda_as_cotas()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            catalogo.Argamassa.EspessuraAcimaOndaMm =
                catalogo.Argamassa.EspessuraAcimaOndaMm.ComValor(60.0, OrigemValor.Conferido, "Teste");
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Umida, CotaMm = 3000.0 };

            CamadasLaje c = CamadasLaje.Calcular(parametros, catalogo);

            Assert.Equal(2900.0, c.TopoVigaMm, 6);
            Assert.Equal(2810.0, c.BaseVigaMm, 6);
        }

        [Fact]
        public void Camada_inexistente_retorna_nulo()
        {
            CamadasLaje c = CamadasLaje.CalcularUmida(3000.0, 90.0, 40.0, 50.0);

            Assert.Null(c.Camada(CamadaLaje.NomePisoSeco));
            Assert.Null(c.Camada("Inexistente"));
        }

        [Fact]
        public void Parametros_ou_catalogo_nulos_lancam_excecao()
        {
            Assert.Throws<ArgumentNullException>(() => CamadasLaje.Calcular(null, CatalogoLaje.CriarPadrao()));
            Assert.Throws<ArgumentNullException>(() => CamadasLaje.Calcular(new ParametrosLaje(), null));

            var semPerfil = new ParametrosLaje { Perfil = null };
            Assert.Throws<ArgumentException>(() => CamadasLaje.Calcular(semPerfil, CatalogoLaje.CriarPadrao()));
        }

        [Fact]
        public void Camada_com_base_acima_do_topo_e_rejeitada()
        {
            Assert.Throws<ArgumentException>(() => new CamadaLaje("X", 100.0, 200.0));
            Assert.Throws<ArgumentException>(() => new CamadaLaje("  ", 200.0, 100.0));
        }

        // ---------- Regressao da correcao dirigida ----------

        [Fact]
        public void Perfil_com_outra_alma_usa_LarguraAlmaMm_como_altura_da_viga()
        {
            var perfil = new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = 200.0,
                EspessuraChapaMm = 0.95
            };
            var umida = new ParametrosLaje { Tipo = TipoLaje.Umida, CotaMm = 3000.0, Perfil = perfil };
            var seca = new ParametrosLaje { Tipo = TipoLaje.Seca, CotaMm = 3000.0, Perfil = perfil };
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();

            CamadasLaje camadasUmida = CamadasLaje.Calcular(umida, catalogo);
            CamadasLaje camadasSeca = CamadasLaje.Calcular(seca, catalogo);

            Assert.Equal(2710.0, camadasUmida.BaseVigaMm, 6);
            Assert.Equal(2782.0, camadasSeca.BaseVigaMm, 6);
            Assert.Equal(200.0, camadasUmida.Camada(CamadaLaje.NomeViga).EspessuraMm, 6);
            Assert.NotEqual(perfil.AbaMm, camadasUmida.Camada(CamadaLaje.NomeViga).EspessuraMm);
        }

        [Fact]
        public void Perfil_com_alma_nao_positiva_lanca_excecao()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            var zero = new ParametrosLaje
            {
                CotaMm = 3000.0,
                Perfil = new PerfilSpec { Sistema = SistemaPerfil.SteelFrame, LarguraAlmaMm = 0.0, EspessuraChapaMm = 0.95 }
            };
            var negativa = new ParametrosLaje
            {
                CotaMm = 3000.0,
                Perfil = new PerfilSpec { Sistema = SistemaPerfil.SteelFrame, LarguraAlmaMm = -90.0, EspessuraChapaMm = 0.95 }
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.Calcular(zero, catalogo));
            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.Calcular(negativa, catalogo));
        }

        [Fact]
        public void Catalogo_com_altura_de_onda_nao_positiva_lanca_excecao_na_laje_umida()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            catalogo.Telha.AlturaOndaMm = catalogo.Telha.AlturaOndaMm.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Umida, CotaMm = 3000.0 };

            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.Calcular(parametros, catalogo));
        }

        [Fact]
        public void Catalogo_com_argamassa_nao_positiva_lanca_excecao_na_laje_umida()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            catalogo.Argamassa.EspessuraAcimaOndaMm =
                catalogo.Argamassa.EspessuraAcimaOndaMm.ComValor(-10.0, OrigemValor.Provisorio, "Teste");
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Umida, CotaMm = 3000.0 };

            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.Calcular(parametros, catalogo));
        }

        [Fact]
        public void Catalogo_com_piso_seco_de_espessura_zero_lanca_excecao_na_laje_seca()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            catalogo.PisoSeco.EspessuraMm = catalogo.PisoSeco.EspessuraMm.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Seca, CotaMm = 3000.0 };

            Assert.Throws<ArgumentOutOfRangeException>(() => CamadasLaje.Calcular(parametros, catalogo));
        }

        [Fact]
        public void Laje_seca_ignora_o_catalogo_da_telha_e_da_argamassa()
        {
            CatalogoLaje catalogo = CatalogoLaje.CriarPadrao();
            catalogo.Telha.AlturaOndaMm = catalogo.Telha.AlturaOndaMm.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            catalogo.Argamassa.EspessuraAcimaOndaMm =
                catalogo.Argamassa.EspessuraAcimaOndaMm.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            var parametros = new ParametrosLaje { Tipo = TipoLaje.Seca, CotaMm = 3000.0 };

            CamadasLaje c = CamadasLaje.Calcular(parametros, catalogo);

            Assert.Equal(2892.0, c.BaseVigaMm, 6);
        }

        [Fact]
        public void Valor_nao_finito_nao_pode_existir_no_catalogo()
        {
            Assert.Throws<ArgumentException>(
                () => new ValorCatalogo("X", double.NaN, "mm", OrigemValor.Provisorio, "Teste"));
            Assert.Throws<ArgumentException>(
                () => new ValorCatalogo("X", double.PositiveInfinity, "mm", OrigemValor.Provisorio, "Teste"));

            ValorCatalogo valido = new ValorCatalogo("X", 1.0, "mm", OrigemValor.Provisorio, "Teste");
            Assert.Throws<ArgumentException>(() => valido.ComValor(double.NaN, OrigemValor.Provisorio, "Teste"));
        }
    }
}
