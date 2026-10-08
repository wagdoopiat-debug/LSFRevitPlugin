using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    /// <summary>
    /// Codec do texto de Comentarios, que e o canal legado de identidade dos elementos de
    /// estrutura. Precisa continuar compativel com o texto que a geracao grava, porque e ele
    /// que da identidade aos elementos criados antes do Extensible Storage existir.
    /// </summary>
    public class IdentidadeEstruturalTests
    {
        private const string Perfil = "Drywall 90mm, chapa 0.50mm (aba 40mm, labio 15mm)";

        [Fact]
        public void TextoDaGeracao_Volta_Parede_Painel_Tipo_EPerfil()
        {
            IdentidadeEstrutural identidade;

            bool ok = IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 1234 | Painel 3 | Montante principal | " + Perfil,
                out identidade);

            Assert.True(ok);
            Assert.Equal(1234, identidade.ParedeId);
            Assert.Equal(3, identidade.NumeroPainel);
            Assert.Equal("Montante principal", identidade.Tipo);
            Assert.Equal(Perfil, identidade.Perfil);
        }

        [Fact]
        public void MontarTexto_ETentarExtrair_FazemRoundTrip()
        {
            IdentidadeEstrutural identidade;

            bool ok = IdentidadeEstrutural.TentarExtrair(
                IdentidadeEstrutural.MontarTexto(98765, 12, "Guia inferior", Perfil),
                out identidade);

            Assert.True(ok);
            Assert.Equal(98765, identidade.ParedeId);
            Assert.Equal(12, identidade.NumeroPainel);
            Assert.Equal("Guia inferior", identidade.Tipo);
            Assert.Equal(Perfil, identidade.Perfil);
        }

        [Fact]
        public void MontarTexto_UsaOMesmoFormatoDaGeracao()
        {
            // Mesmo texto que PrefixoEtiqueta + "Painel n | tipo | perfil" produz.
            Assert.Equal(
                "LSF | Parede 1234 | Painel 2 | Cripple stud | " + Perfil,
                IdentidadeEstrutural.MontarTexto(1234, 2, "Cripple stud", Perfil));
        }

        [Fact]
        public void Parede1234_NaoEFlagradaComoParede12345()
        {
            IdentidadeEstrutural identidade;

            Assert.True(IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 1234 | Painel 1 | Montante principal | " + Perfil, out identidade));
            Assert.Equal(1234, identidade.ParedeId);

            Assert.True(IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 12345 | Painel 1 | Montante principal | " + Perfil, out identidade));
            Assert.Equal(12345, identidade.ParedeId);
        }

        [Fact]
        public void TextoQueNaoEEtiquetaDoPlugin_NaoGeraIdentidade()
        {
            IdentidadeEstrutural identidade;

            Assert.False(IdentidadeEstrutural.TentarExtrair("Alguma anotacao do usuario", out identidade));
            Assert.False(IdentidadeEstrutural.TentarExtrair("LSF Placa - Gesso ST", out identidade));
            Assert.Null(identidade);
        }

        [Fact]
        public void TextoVazioOuNulo_NaoGeraIdentidade()
        {
            IdentidadeEstrutural identidade;

            Assert.False(IdentidadeEstrutural.TentarExtrair(null, out identidade));
            Assert.False(IdentidadeEstrutural.TentarExtrair("", out identidade));
            Assert.Null(identidade);
        }

        [Fact]
        public void SemTipoESemPerfil_NaoGeraIdentidade()
        {
            // "LSF | Parede 1234 | Painel 1" tem 3 campos; a leitura antiga exigia 4.
            IdentidadeEstrutural identidade;

            Assert.False(IdentidadeEstrutural.TentarExtrair("LSF | Parede 1234 | Painel 1", out identidade));
        }

        [Fact]
        public void IdDaParedeIlegivel_NaoGeraIdentidade()
        {
            IdentidadeEstrutural identidade;

            Assert.False(IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede xxx | Painel 1 | Montante principal | " + Perfil, out identidade));
        }

        [Fact]
        public void NumeroDoPainelIlegivel_CaiParaZero()
        {
            // O painel deixa de ser conhecido, mas o elemento continua pertencecendo a parede.
            IdentidadeEstrutural identidade;

            bool ok = IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 1234 | Painel x | Montante principal | " + Perfil,
                out identidade);

            Assert.True(ok);
            Assert.Equal(1234, identidade.ParedeId);
            Assert.Equal(0, identidade.NumeroPainel);
        }

        [Fact]
        public void SemPerfil_NoTexto_PerfilFicaVazio()
        {
            IdentidadeEstrutural identidade;

            bool ok = IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 1234 | Painel 1 | Montante principal", out identidade);

            Assert.True(ok);
            Assert.Equal("Montante principal", identidade.Tipo);
            Assert.Equal("", identidade.Perfil);
        }

        [Fact]
        public void PerfilComSeparadorDentro_VoltaJuntado()
        {
            IdentidadeEstrutural identidade;

            bool ok = IdentidadeEstrutural.TentarExtrair(
                "LSF | Parede 1234 | Painel 1 | Reforco de vao | Drywall 90mm | chapa 0.50mm",
                out identidade);

            Assert.True(ok);
            Assert.Equal("Reforco de vao", identidade.Tipo);
            Assert.Equal("Drywall 90mm | chapa 0.50mm", identidade.Perfil);
        }
    }
}