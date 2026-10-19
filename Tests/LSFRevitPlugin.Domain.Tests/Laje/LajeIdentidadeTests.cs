using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeIdentidadeTests
    {
        [Fact]
        public void MontarTexto_segue_o_formato_do_prefixo()
        {
            string texto = IdentidadeLajeCodec.MontarTexto(1, TipoLaje.Umida, PapelElementoLaje.Viga, 0);

            Assert.Equal("LSF-LAJE | Laje 1 | Umida | Viga | 0", texto);
        }

        [Fact]
        public void Texto_ida_e_volta_preserva_a_identidade()
        {
            string texto = IdentidadeLajeCodec.MontarTexto(42, TipoLaje.Seca, PapelElementoLaje.GuiaBorda, 7);

            IdentidadeLaje identidade;
            Assert.True(IdentidadeLajeCodec.TentarExtrair(texto, out identidade));
            Assert.Equal(42, identidade.LajeId);
            Assert.Equal(TipoLaje.Seca, identidade.Tipo);
            Assert.Equal(PapelElementoLaje.GuiaBorda, identidade.Papel);
            Assert.Equal(7, identidade.Numero);
        }

        [Theory]
        [InlineData(PapelElementoLaje.Travamento)]
        [InlineData(PapelElementoLaje.Fita)]
        public void Texto_ida_e_volta_preserva_papeis_do_travamento(PapelElementoLaje papel)
        {
            string texto = IdentidadeLajeCodec.MontarTexto(9, TipoLaje.Seca, papel, 3);

            IdentidadeLaje identidade;
            Assert.True(IdentidadeLajeCodec.TentarExtrair(texto, out identidade));
            Assert.Equal(9, identidade.LajeId);
            Assert.Equal(papel, identidade.Papel);
            Assert.Equal(3, identidade.Numero);
        }

        [Theory]
        [InlineData("")]
        [InlineData("LSF | Parede 12 | Painel 1 | Montante | ...")]
        [InlineData("LSF-LAJE | sem id | Umida | Viga | 0")]
        [InlineData("LSF-LAJE | Laje 3 | Invalido | Viga | 0")]
        [InlineData("LSF-LAJE | Laje 3 | Umida | Invalido | 0")]
        [InlineData("LSF-LAJE | Laje 3")]
        public void Texto_invalido_nao_e_interpretado(string comentario)
        {
            IdentidadeLaje identidade;
            Assert.False(IdentidadeLajeCodec.TentarExtrair(comentario, out identidade));
            Assert.Null(identidade);
        }

        [Fact]
        public void Prefixo_da_laje_nao_colide_com_o_da_parede()
        {
            string textoParede = LSFRevitPlugin.Domain.Models.IdentidadeEstrutural.MontarTexto(
                10, 1, "Montante", "SF 90");

            IdentidadeLaje identidade;
            Assert.False(IdentidadeLajeCodec.TentarExtrair(textoParede, out identidade));
        }
    }
}
