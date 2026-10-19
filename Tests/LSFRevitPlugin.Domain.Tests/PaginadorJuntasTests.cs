using System;
using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class PaginadorJuntasTests
    {
        private static readonly MedidaPlaca Medida = new MedidaPlaca(1200, 2400);

        private static List<double> Grade(double largura, double passo)
        {
            var lista = new List<double>();
            for (double u = 0; u <= largura + 0.5; u += passo) lista.Add(u);
            return lista;
        }

        private static EntradaPaginacao Entrada(
            double largura, double altura, List<double> montantes, List<RetanguloMm> aberturas,
            OrientacaoPlaca orientacao = OrientacaoPlaca.EmPe)
        {
            return new EntradaPaginacao
            {
                NumeroPainel = 1,
                NumeroFace = 1,
                LarguraPainelMm = largura,
                AlturaMm = altura,
                PosicoesMontantesMm = montantes,
                Aberturas = aberturas,
                Tipo = TipoPlaca.GessoSt,
                Medida = Medida,
                Orientacao = orientacao,

                // Estes testes foram escritos para 30 cm de distancia das quinas das aberturas e
                // para a peca minima de 100 mm: fixam os valores para nao depender do padrao.
                LarguraMinimaPecaMm = 100.0,
                DistanciaMinimaVerticeMm = 300.0
            };
        }

        [Fact]
        public void JuntaFicaA30cmDosVerticesDaAbertura_MesmoComJackEKingPerto()
        {
            // Jack (1180) e king (1200) colados na quina da abertura, como na estrutura real.
            var montantes = new List<double> { 0, 400, 800, 1180, 1200, 1600, 2000, 2400 };
            var janela = new List<RetanguloMm> { new RetanguloMm(1200, 1700, 900, 2000) };

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(2400, 2400, montantes, janela), estoque);

            Assert.Equal(new List<double> { 800, 2000 }, r.JuntasVerticaisPorFiada[0]);

            foreach (double junta in r.JuntasVerticaisPorFiada[0])
            {
                Assert.True(Math.Abs(junta - 1200) >= 299.5);
                Assert.True(Math.Abs(junta - 1700) >= 299.5);
            }

            Assert.DoesNotContain(r.Avisos, a => a.Contains("fora de montante") || a.Contains("quina"));
        }

        [Fact]
        public void JuntasNaoFicamNaLinhaDasQuinasDaPorta_EmNenhumaFiada()
        {
            // Parede de 2700 com placa em pe de 2400: duas fiadas. King e jack ficam na quina da
            // porta (1000 e 1900) e a faixa de cima nao pode ter junta sobre a linha da quina.
            var montantes = Grade(3600, 400);
            montantes.AddRange(new List<double> { 980, 1020, 1880, 1920 });
            montantes.Sort();

            var porta = new List<RetanguloMm> { new RetanguloMm(1000, 1900, 0, 2100) };

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(3600, 2700, montantes, porta), estoque);

            Assert.Equal(2, r.JuntasVerticaisPorFiada.Count);

            foreach (List<double> fiada in r.JuntasVerticaisPorFiada)
            {
                foreach (double junta in fiada)
                {
                    Assert.True(Math.Abs(junta - 1000) >= 299.5, "junta " + junta + " perto da quina em 1000");
                    Assert.True(Math.Abs(junta - 1900) >= 299.5, "junta " + junta + " perto da quina em 1900");
                }
            }
        }

        [Fact]
        public void JuntaPrefereOMeioDoVaoQuandoHaMontanteLa()
        {
            var porta = new List<RetanguloMm> { new RetanguloMm(1000, 2000, 0, 2100) };

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(3600, 2415, Grade(3600, 400), porta), estoque);

            List<double> juntas = r.JuntasVerticaisPorFiada[0];

            // Uma junta cai dentro do vao (entre 30 cm das duas quinas) e nenhuma fica perto delas.
            Assert.Contains(juntas, j => j >= 1300 - 0.5 && j <= 1700 + 0.5);
            foreach (double junta in juntas)
            {
                Assert.True(Math.Abs(junta - 1000) >= 299.5);
                Assert.True(Math.Abs(junta - 2000) >= 299.5);
            }
        }

        [Fact]
        public void SemMontanteAfastado_UsaOMaisPertoEAvisa()
        {
            var montantes = new List<double> { 0, 1200, 2400 };
            var abertura = new List<RetanguloMm> { new RetanguloMm(1200, 1700, 0, 2100) };

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(Entrada(2400, 2400, montantes, abertura), estoque);

            Assert.Equal(new List<double> { 1200 }, r.JuntasVerticaisPorFiada[0]);
            Assert.Contains(r.Avisos, a => a.Contains("quina"));
        }

        [Fact]
        public void DistanciaZero_DesligaARegraDosVertices()
        {
            var montantes = new List<double> { 0, 400, 800, 1180, 1200, 1600, 2000, 2400 };
            var janela = new List<RetanguloMm> { new RetanguloMm(1200, 1700, 900, 2000) };

            EntradaPaginacao entrada = Entrada(2400, 2400, montantes, janela);
            entrada.DistanciaMinimaVerticeMm = 0;

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(entrada, estoque);

            Assert.Equal(new List<double> { 1200 }, r.JuntasVerticaisPorFiada[0]);
        }

        [Fact]
        public void JuntaHorizontalSobeOuDesceParaFicarA30cmDasArestasDaJanela()
        {
            // Deitada: a junta horizontal cairia em 1215 mm, a 115 mm do peitoril (1100).
            var janela = new List<RetanguloMm> { new RetanguloMm(600, 1000, 1100, 2000) };

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(3000, 2700, Grade(3000, 400), janela, OrientacaoPlaca.Deitada), estoque);

            PecaPlaca primeiraFiada = r.Pecas.First(p => p.Fiada == 1);
            double topoDaPrimeiraFiada = primeiraFiada.Contorno.Z1;

            Assert.True(Math.Abs(topoDaPrimeiraFiada - 1100) >= 299.5);
            Assert.True(Math.Abs(topoDaPrimeiraFiada - 2000) >= 299.5);
        }

        [Fact]
        public void PlacasComecam15mmAcimaDoPiso_EmTodosOsTipos()
        {
            foreach (TipoPlaca tipo in PlacaCatalogo.TodosOsTipos())
            {
                Assert.Equal(15, PlacaCatalogo.RegraDoTipo(tipo).FolgaDoPisoMm, 3);
            }

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(1200, 2400, Grade(1200, 400), new List<RetanguloMm>()), estoque);

            Assert.All(r.Pecas, p => Assert.Equal(15, p.Contorno.Z0, 3));
        }

        [Fact]
        public void PrefixoDoCodigo_DefineOCodigoDasPecas()
        {
            EntradaPaginacao entrada = Entrada(2400, 2400, Grade(2400, 400), new List<RetanguloMm>());
            entrada.PrefixoCodigo = "1234-P2";

            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, Medida);
            ResultadoPaginacao r = Paginador.Paginar(entrada, estoque);

            Assert.Equal("1234-P2-F1-01", r.Pecas[0].Codigo);
            Assert.Equal("1234-P2-F1-02", r.Pecas[1].Codigo);
        }
    }
}