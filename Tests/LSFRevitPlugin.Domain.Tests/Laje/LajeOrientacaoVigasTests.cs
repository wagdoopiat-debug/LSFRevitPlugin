using System;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeOrientacaoVigasTests
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

        [Fact]
        public void Retangulo_4x6_vigas_correm_ao_longo_do_lado_curto()
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(Retangulo4x6()), null);

            Assert.True(o.EhAutomatica);
            Assert.Equal(90.0, o.AnguloUGraus, 6);
            Assert.Equal(4000.0, o.ExtensaoUMm, 6);
            Assert.Equal(6000.0, o.ExtensaoVMm, 6);
            Assert.Equal(4000.0, o.VaoMaximoMm, 6);
            Assert.Equal(0.0, o.UX, 9);
            Assert.Equal(1.0, o.UY, 9);
        }

        [Fact]
        public void Retangulo_girado_30_graus_da_o_mesmo_resultado_relativo()
        {
            double c = Math.Cos(Math.PI / 6.0);
            double s = Math.Sin(Math.PI / 6.0);
            Func<double, double, Ponto2DMm> girar = (x, y) => new Ponto2DMm(x * c - y * s, x * s + y * c);
            var pontos = new[] { girar(0, 0), girar(6000, 0), girar(6000, 4000), girar(0, 4000) };

            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(pontos), null);

            Assert.Equal(120.0, o.AnguloUGraus, 6);
            Assert.Equal(4000.0, o.ExtensaoUMm, 6);
            Assert.Equal(6000.0, o.ExtensaoVMm, 6);
        }

        [Fact]
        public void Direcao_manual_0_graus_poe_as_vigas_ao_longo_de_X()
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(Retangulo4x6()), 0.0);

            Assert.False(o.EhAutomatica);
            Assert.Equal(0.0, o.AnguloUGraus, 6);
            Assert.Equal(6000.0, o.ExtensaoUMm, 6);
            Assert.Equal(1.0, o.UX, 9);
            Assert.Equal(0.0, o.UY, 9);
        }

        [Fact]
        public void Direcao_manual_90_graus_inverte_em_relacao_a_0()
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(Retangulo4x6()), 90.0);

            Assert.Equal(4000.0, o.ExtensaoUMm, 6);
            Assert.True(Math.Abs(o.UX) < 1e-9);
            Assert.Equal(1.0, o.UY, 9);
        }

        [Theory]
        [InlineData(-90.0, 90.0)]
        [InlineData(270.0, 90.0)]
        [InlineData(180.0, 0.0)]
        [InlineData(45.0, 45.0)]
        [InlineData(360.0, 0.0)]
        public void Direcao_manual_e_normalizada_para_0_a_180(double informada, double esperada)
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(Retangulo4x6()), informada);

            Assert.Equal(esperada, o.AnguloUGraus, 6);
        }

        [Fact]
        public void Empate_usa_a_direcao_da_aresta_mais_longa()
        {
            var quadrado = new[] { P(0, 0), P(5000, 0), P(5000, 5000), P(0, 5000) };

            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(quadrado), null);

            Assert.Equal(0.0, o.AnguloUGraus, 6);
        }

        [Fact]
        public void Contorno_em_L_usa_o_retangulo_envolvente()
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(new ContornoLaje(FormaEmL()), null);

            Assert.Equal(90.0, o.AnguloUGraus, 6);
            Assert.Equal(4000.0, o.ExtensaoUMm, 6);
            Assert.Equal(6000.0, o.ExtensaoVMm, 6);
            Assert.Equal(4000.0, o.VaoMaximoMm, 6);
        }

        [Fact]
        public void Entradas_invalidas_lancam_excecao()
        {
            Assert.Throws<ArgumentNullException>(() => OrientacaoVigas.Calcular(null, null));

            var gravata = new ContornoLaje(new[] { P(0, 0), P(1000, 1000), P(1000, 0), P(0, 1000) });
            Assert.Throws<ArgumentException>(() => OrientacaoVigas.Calcular(gravata, null));

            var retangulo = new ContornoLaje(Retangulo4x6());
            Assert.Throws<ArgumentException>(() => OrientacaoVigas.Calcular(retangulo, double.NaN));
        }

        [Fact]
        public void Coordenadas_uv_fazem_ida_e_volta_ao_plano_da_planta()
        {
            var contorno = new ContornoLaje(FormaEmL());
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, 30.0);

            foreach (Ponto2DMm p in contorno.Pontos)
            {
                Ponto2DMm q = o.ParaMundo(o.CoordenadaU(p), o.CoordenadaV(p));

                Assert.Equal(p.XMm, q.XMm, 6);
                Assert.Equal(p.YMm, q.YMm, 6);
            }
        }
    }
}
