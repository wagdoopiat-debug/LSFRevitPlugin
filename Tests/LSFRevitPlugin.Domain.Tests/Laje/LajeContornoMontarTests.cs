using System.Collections.Generic;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeContornoMontarTests
    {
        private static SegmentoContorno Seg(double ax, double ay, double bx, double by)
        {
            return new SegmentoContorno(new Ponto2DMm(ax, ay), new Ponto2DMm(bx, by));
        }

        [Fact]
        public void Monta_retangulo_a_partir_de_segmentos_embaralhados()
        {
            var segmentos = new[]
            {
                Seg(6000, 4000, 0, 4000),   // invertido
                Seg(0, 0, 6000, 0),
                Seg(0, 4000, 0, 0),         // invertido
                Seg(6000, 0, 6000, 4000)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Empty(erros);
            Assert.NotNull(c);
            Assert.True(c.EhValido);
            Assert.Equal(6000.0 * 4000.0, c.AreaMm2, 3);
            Assert.Equal(4, c.Pontos.Count);
        }

        [Fact]
        public void Cadeia_aberta_gera_erro()
        {
            var segmentos = new[]
            {
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 1000, 1000),
                Seg(1000, 1000, 0, 1000)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Null(c);
            Assert.Contains(erros, e => e.Contains("aberto"));
        }

        [Fact]
        public void Ramificacao_gera_erro()
        {
            var segmentos = new[]
            {
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 1000, 1000),
                Seg(1000, 1000, 0, 0),
                Seg(0, 0, 500, 500) // terceira borda no vertice (0,0)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Null(c);
            Assert.Contains(erros, e => e.Contains("ramificam"));
        }

        [Fact]
        public void Menos_de_tres_bordas_gera_erro()
        {
            var segmentos = new[]
            {
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 1000, 1000)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Null(c);
            Assert.Contains(erros, e => e.Contains("pelo menos 3"));
        }

        [Fact]
        public void Borda_degenerada_e_descartada()
        {
            var segmentos = new[]
            {
                Seg(0, 0, 0, 0), // degenerada
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 1000, 1000),
                Seg(1000, 1000, 0, 0)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Contains(erros, e => e.Contains("desprezivel"));
            Assert.NotNull(c);
            Assert.True(c.EhValido);
            Assert.Equal(3, c.Pontos.Count);
        }

        [Fact]
        public void Dois_contornos_separados_geram_erro()
        {
            var segmentos = new[]
            {
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 1000, 1000),
                Seg(1000, 1000, 0, 1000),
                Seg(0, 1000, 0, 0),
                Seg(5000, 0, 6000, 0),
                Seg(6000, 0, 6000, 1000),
                Seg(6000, 1000, 5000, 1000),
                Seg(5000, 1000, 5000, 0)
            };

            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            Assert.Null(c);
            Assert.Contains(erros, e => e.Contains("unico contorno"));
        }

        [Fact]
        public void Segmentos_nulos_geram_erro_sem_excecao()
        {
            List<string> erros;
            ContornoLaje c = ContornoLaje.MontarDeSegmentos(null, out erros);

            Assert.Null(c);
            Assert.NotEmpty(erros);
        }
    }
}
