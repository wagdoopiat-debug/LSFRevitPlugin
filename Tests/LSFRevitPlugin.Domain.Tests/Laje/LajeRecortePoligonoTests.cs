using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes das utilidades puras de poligono (RecortePoligono): area, convexidade, ponto-dentro,
    /// intervalos verticais e recorte por celula (convexo e concavo/desconexo).
    /// </summary>
    public class LajeRecortePoligonoTests
    {
        private static List<Ponto2DMm> Quadrado(double x0, double y0, double x1, double y1)
        {
            return new List<Ponto2DMm>
            {
                new Ponto2DMm(x0, y0),
                new Ponto2DMm(x1, y0),
                new Ponto2DMm(x1, y1),
                new Ponto2DMm(x0, y1)
            };
        }

        /// <summary>Retangulo com um entalhe retangular na borda direita (contorno concavo).</summary>
        private static List<Ponto2DMm> ContornoComEntalhe()
        {
            return new List<Ponto2DMm>
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(100, 0),
                new Ponto2DMm(100, 40), new Ponto2DMm(60, 40),
                new Ponto2DMm(60, 60), new Ponto2DMm(100, 60),
                new Ponto2DMm(100, 100), new Ponto2DMm(0, 100)
            };
        }

        [Fact]
        public void Area_de_retangulo()
        {
            Assert.Equal(5000.0, RecortePoligono.Area(Quadrado(0, 0, 100, 50)), 6);
        }

        [Fact]
        public void Convexidade_distingue_retangulo_de_contorno_entalhado()
        {
            Assert.True(RecortePoligono.EhConvexo(Quadrado(0, 0, 100, 50)));
            Assert.False(RecortePoligono.EhConvexo(ContornoComEntalhe()));
        }

        [Fact]
        public void Contem_identifica_dentro_fora_e_borda()
        {
            List<Ponto2DMm> q = Quadrado(0, 0, 100, 50);

            Assert.True(RecortePoligono.Contem(q, new Ponto2DMm(50, 25)));
            Assert.False(RecortePoligono.Contem(q, new Ponto2DMm(150, 25)));
            Assert.True(RecortePoligono.Contem(q, new Ponto2DMm(0, 25)));

            Assert.False(RecortePoligono.Contem(ContornoComEntalhe(), new Ponto2DMm(80, 50)));
        }

        [Fact]
        public void Intervalos_v_de_retangulo()
        {
            List<(double Inicio, double Fim)> dentro =
                RecortePoligono.IntervalosV(Quadrado(0, 0, 100, 50), 50.0);
            List<(double Inicio, double Fim)> fora =
                RecortePoligono.IntervalosV(Quadrado(0, 0, 100, 50), 150.0);

            Assert.Single(dentro);
            Assert.Equal(0.0, dentro[0].Inicio, 6);
            Assert.Equal(50.0, dentro[0].Fim, 6);
            Assert.Empty(fora);
        }

        [Fact]
        public void Intervalos_v_no_entalhe_tem_dois_trechos()
        {
            List<(double Inicio, double Fim)> intervalos =
                RecortePoligono.IntervalosV(ContornoComEntalhe(), 80.0);

            Assert.Equal(2, intervalos.Count);
            Assert.Equal(0, intervalos[0].Inicio, 6);
            Assert.Equal(40, intervalos[0].Fim, 6);
            Assert.Equal(60, intervalos[1].Inicio, 6);
            Assert.Equal(100, intervalos[1].Fim, 6);
        }

        [Fact]
        public void Recortar_sujeito_por_janela_convexa()
        {
            List<Ponto2DMm> recorte = RecortePoligono.Recortar(
                Quadrado(0, 0, 100, 100), Quadrado(25, 25, 75, 75));

            Assert.True(recorte.Count >= 3);
            Assert.Equal(2500.0, RecortePoligono.Area(recorte), 3);
        }

        [Fact]
        public void Triangulacao_conserva_a_area_do_contorno_entalhado()
        {
            List<Ponto2DMm> c = ContornoComEntalhe();
            List<List<Ponto2DMm>> triangulos = RecortePoligono.Triangular(c);

            Assert.Equal(c.Count - 2, triangulos.Count);
            double soma = triangulos.Sum(RecortePoligono.Area);
            Assert.Equal(RecortePoligono.Area(c), soma, 3);
        }

        [Fact]
        public void Recorte_por_celula_de_contorno_convexo_devolve_um_pedaco()
        {
            List<List<Ponto2DMm>> pedacos = RecortePoligono.RecortarContornoPorCelula(
                Quadrado(0, 0, 100, 100), Quadrado(10, 10, 40, 40));

            Assert.Single(pedacos);
            Assert.Equal(900.0, RecortePoligono.Area(pedacos[0]), 3);
        }

        [Fact]
        public void Recorte_por_celula_de_contorno_concavo_desconexo_devolve_dois_pedacos()
        {
            List<List<Ponto2DMm>> pedacos = RecortePoligono.RecortarContornoPorCelula(
                ContornoComEntalhe(), Quadrado(65, 0, 100, 100));

            Assert.Equal(2, pedacos.Count);
            double soma = pedacos.Sum(RecortePoligono.Area);
            Assert.Equal(2 * 35.0 * 40.0, soma, 3);
        }

        [Fact]
        public void Recorte_por_celula_fora_nao_devolve_pedaco()
        {
            List<List<Ponto2DMm>> pedacos = RecortePoligono.RecortarContornoPorCelula(
                Quadrado(0, 0, 10, 10), Quadrado(100, 100, 200, 200));

            Assert.Empty(pedacos);
        }
    }
}
