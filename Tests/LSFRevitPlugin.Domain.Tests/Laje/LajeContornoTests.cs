using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeContornoTests
    {
        private static Ponto2DMm P(double x, double y)
        {
            return new Ponto2DMm(x, y);
        }

        private static Ponto2DMm[] Retangulo4x6()
        {
            return new[] { P(0, 0), P(6000, 0), P(6000, 4000), P(0, 4000) };
        }

        private static Ponto2DMm[] FormaEmL()
        {
            return new[] { P(0, 0), P(6000, 0), P(6000, 2000), P(2000, 2000), P(2000, 4000), P(0, 4000) };
        }

        private static Ponto2DMm[] FormaEmU()
        {
            return new[]
            {
                P(0, 0), P(6000, 0), P(6000, 4000), P(4000, 4000),
                P(4000, 1000), P(2000, 1000), P(2000, 4000), P(0, 4000)
            };
        }

        [Fact]
        public void Retangulo_4x6_tem_area_24_m2()
        {
            var contorno = new ContornoLaje(Retangulo4x6());

            Assert.Equal(24000000.0, contorno.AreaMm2, 3);
            Assert.Equal(24.0, contorno.AreaM2, 6);
        }

        [Fact]
        public void Contorno_em_L_tem_area_16_m2()
        {
            var contorno = new ContornoLaje(FormaEmL());

            Assert.Equal(16.0, contorno.AreaM2, 6);
        }

        [Fact]
        public void Sentido_horario_vira_anti_horario()
        {
            var horario = new[] { P(0, 0), P(0, 4000), P(6000, 4000), P(6000, 0) };

            var contorno = new ContornoLaje(horario);

            Assert.True(contorno.AreaAssinadaMm2 > 0.0);
            Assert.Equal(24000000.0, contorno.AreaMm2, 3);
            Assert.Equal(4, contorno.Pontos.Count);
        }

        [Fact]
        public void Ponto_de_fechamento_repetido_e_removido()
        {
            var pontos = Retangulo4x6().Concat(new[] { P(0, 0) });

            var contorno = new ContornoLaje(pontos);

            Assert.Equal(4, contorno.Pontos.Count);
            Assert.True(contorno.EhValido);
        }

        [Fact]
        public void Menos_de_tres_pontos_distintos_e_rejeitado()
        {
            var doisPontos = new ContornoLaje(new[] { P(0, 0), P(1000, 0) });
            var tresComRepetido = new ContornoLaje(new[] { P(0, 0), P(1000, 0), P(1000, 0) });

            Assert.Contains(doisPontos.Validar(), e => e.Contains("pelo menos 3 pontos"));
            Assert.Contains(tresComRepetido.Validar(), e => e.Contains("pelo menos 3 pontos"));
        }

        [Fact]
        public void Gravata_e_rejeitada()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(1000, 1000), P(1000, 0), P(0, 1000) });

            Assert.Contains(contorno.Validar(), e => e.Contains("autointerseccao"));
        }

        [Fact]
        public void Pontos_colineares_sao_rejeitados()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(1000, 0), P(2000, 0) });

            Assert.False(contorno.EhValido);
        }

        [Fact]
        public void Vertice_tocando_outra_aresta_e_rejeitado()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(4000, 0), P(4000, 4000), P(2000, 0), P(0, 4000) });

            Assert.Contains(contorno.Validar(), e => e.Contains("autointerseccao"));
        }

        [Fact]
        public void Contorno_desprezivel_e_rejeitado_por_area()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(0.5, 0), P(0, 0.5) });

            Assert.Contains(contorno.Validar(), e => e.Contains("area"));
        }

        [Fact]
        public void Retangulo_L_e_U_sao_validos()
        {
            Assert.True(new ContornoLaje(Retangulo4x6()).EhValido);
            Assert.True(new ContornoLaje(FormaEmL()).EhValido);

            var u = new ContornoLaje(FormaEmU());
            Assert.True(u.EhValido);
            Assert.Equal(18.0, u.AreaM2, 6);
        }
    }
}
