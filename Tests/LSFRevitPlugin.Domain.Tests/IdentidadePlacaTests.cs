using System.Globalization;
using System.Threading;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    /// <summary>
    /// Codec do texto de Comentarios de placas (formato legado). Deve permanecer compativel.
    /// </summary>
    public class IdentidadePlacaTests
    {
        public IdentidadePlacaTests()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [Fact]
        public void FormatoValidoCompleto_ExtraiTodosOsCampos()
        {
            string comentario = "LSF-PLACA | Parede 3009243 | Painel 1 | Face 1 | Steel Frame 90mm | P-01-01 | 2400 x 2900 mm | placa PT01 | medida 2400x2900 | orientacao Horizontal | area 6960000";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.True(ok);
            Assert.NotNull(identidade);
            Assert.Equal(3009243, identidade.ParedeId);
            Assert.Equal(1, identidade.NumeroPainel);
            Assert.Equal(1, identidade.Face);
            Assert.Equal("Steel Frame 90mm", identidade.TipoPlaca);
            Assert.Equal("Horizontal", identidade.Orientacao);
        }

        [Fact]
        public void FaceDois_ExtraiCorretamente()
        {
            string comentario = "LSF-PLACA | Parede 123 | Painel 2 | Face 2 | OSB 12mm | P-02-01 | 1200 x 2400 mm | placa PT02 | medida 1200x2400 | orientacao Vertical | area 2880000";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.True(ok);
            Assert.Equal(2, identidade.Face);
            Assert.Equal("Vertical", identidade.Orientacao);
        }

        [Fact]
        public void SemOrientacaoPresente_TentaExtrair_RetornaComOrientacaoVazia()
        {
            string comentario = "LSF-PLACA | Parede 100 | Painel 1 | Face 1 | OSB 15mm | P-03-01 | 1220 x 2440 mm | placa PT03 | medida 1220x2440 | area 2976800 | recorte R1 200 x 200 mm";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.True(ok);
            Assert.NotNull(identidade);
            Assert.Equal(100, identidade.ParedeId);
            Assert.Equal(1, identidade.NumeroPainel);
            Assert.Equal(1, identidade.Face);
            Assert.Equal("OSB 15mm", identidade.TipoPlaca);
            Assert.Equal(string.Empty, identidade.Orientacao);
        }

        [Fact]
        public void Reaproveitada_ComSobraDe_MantemFormatoLegadoValido()
        {
            string comentario = "LSF-PLACA | Parede 200 | Painel 1 | Face 1 | OSB 12mm | P-04-01 | 600 x 2400 mm | placa PT04 (sobra de PT03) | medida 600x2400 | orientacao Horizontal | area 1440000";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.True(ok);
            Assert.NotNull(identidade);
            Assert.Equal(200, identidade.ParedeId);
            Assert.Equal(1, identidade.NumeroPainel);
            Assert.Equal("Horizontal", identidade.Orientacao);
        }

        [Fact]
        public void ComRecortes_FormatoValido_ExtraiIdentidade()
        {
            string comentario = "LSF-PLACA | Parede 150 | Painel 1 | Face 1 | OSB 12mm | P-05-01 | 1200 x 2400 mm | placa PT05 | medida 1200x2400 | orientacao Horizontal | area 2880000 | P-05-01-R1 200 x 100 mm | P-05-01-R2 150 x 80 mm";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.True(ok);
            Assert.NotNull(identidade);
            Assert.Equal(150, identidade.ParedeId);
            Assert.Equal(1, identidade.NumeroPainel);
        }

        [Fact]
        public void PrefixoInvalido_NaoExtrai()
        {
            string comentario = "LSF-PLAC | Parede 100 | Painel 1 | Face 1 | OSB 12mm | P-06-01 | 1200 x 2400 mm | placa PT06";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void ComentarioVazio_NaoExtrai()
        {
            string comentario = "";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void SegmentosInsuficientes_NaoExtrai()
        {
            string comentario = "LSF-PLACA | Parede 100 | Painel 1 | Face 1 | OSB 12mm";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void ParedeInvalida_NaoExtrai()
        {
            string comentario = "LSF-PLACA | Parede abc | Painel 1 | Face 1 | OSB 12mm | P-07-01 | 1200 x 2400 mm | placa PT07";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void PainelInvalido_NaoExtrai()
        {
            string comentario = "LSF-PLACA | Parede 100 | Painel abc | Face 1 | OSB 12mm | P-08-01 | 1200 x 2400 mm | placa PT08";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void FaceInvalida_NaoExtrai()
        {
            string comentario = "LSF-PLACA | Parede 100 | Painel 1 | Face X | OSB 12mm | P-09-01 | 1200 x 2400 mm | placa PT09";
            bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);

            Assert.False(ok);
            Assert.Null(identidade);
        }

        [Fact]
        public void CulturaInvariantCulture_ExtraiCorretamente()
        {
            string comentario = "LSF-PLACA | Parede 100 | Painel 1 | Face 1 | OSB 12mm | P-10-01 | 1200 x 2400 mm | placa PT10 | medida 1200x2400 | orientacao Horizontal | area 2880000";
            CultureInfo culturaAntiga = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
                bool ok = IdentidadePlacaCodec.TentarExtrair(comentario, out IdentidadePlaca identidade);
                Assert.True(ok);
                Assert.Equal(100, identidade.ParedeId);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culturaAntiga;
            }
        }
    }
}
