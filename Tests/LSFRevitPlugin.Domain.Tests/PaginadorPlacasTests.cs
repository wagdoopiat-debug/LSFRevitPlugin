using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LSFRevitPlugin.Domain.Boards;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class PaginadorPlacasTests
    {
        private static List<double> Montantes(double largura, double passo)
        {
            var lista = new List<double>();
            for (double u = 0; u < largura - 0.5; u += passo) lista.Add(u);
            lista.Add(largura);
            return lista;
        }

        private static EntradaPaginacao Entrada(
            TipoPlaca tipo, OrientacaoPlaca orientacao, double largura, double altura,
            int desencontro = 0, List<RetanguloMm>? aberturas = null)
        {
            return new EntradaPaginacao
            {
                NumeroPainel = 1,
                NumeroFace = desencontro + 1,
                LarguraPainelMm = largura,
                AlturaMm = altura,
                PosicoesMontantesMm = Montantes(largura, 400),
                Aberturas = aberturas ?? new List<RetanguloMm>(),
                Tipo = tipo,
                Medida = new MedidaPlaca(1200, 2400),
                Orientacao = orientacao,
                IndiceDeDesencontro = desencontro,

                // Estes testes verificam o mecanismo de juntas e sobras com a peca minima de 100 mm
                // e 30 cm de distancia das quinas; fixam os valores para nao depender do padrao.
                LarguraMinimaPecaMm = 100.0,
                DistanciaMinimaVerticeMm = 300.0
            };
        }

        [Fact]
        public void EmPe_PainelDe2400_GeraDuasPlacasComJuntaNoMontante()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 2400, 2400), estoque);

            Assert.Equal(2, r.Pecas.Count);
            Assert.Single(r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(1200, r.JuntasVerticaisPorFiada[0][0], 3);

            Assert.Single(r.Juntas);
            Assert.True(r.Juntas[0].Vertical);

            // 2400 de parede menos 15 mm de folga do piso.
            Assert.True(Math.Abs(r.Juntas[0].ComprimentoMm - 2385) < 1.0);
            Assert.False(r.Juntas[0].Mista);

            Assert.DoesNotContain(r.Avisos, a => a.Contains("fora de montante"));
            Assert.Equal(2, estoque.PlacasUsadas);
        }

        [Fact]
        public void FacesComDesencontroTemJuntasDiferentesEReaproveitamSobras()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));

            ResultadoPaginacao face1 = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 2400, 2400, 0), estoque);
            ResultadoPaginacao face2 = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 2400, 2400, 1), estoque);

            Assert.Equal(1200, face1.JuntasVerticaisPorFiada[0][0], 3);
            Assert.Equal(2, face2.JuntasVerticaisPorFiada[0].Count);
            Assert.Equal(400, face2.JuntasVerticaisPorFiada[0][0], 3);
            Assert.Equal(1600, face2.JuntasVerticaisPorFiada[0][1], 3);

            double menor = Paginador.MenorDistancia(
                face1.JuntasVerticaisPorFiada[0], face2.JuntasVerticaisPorFiada[0]);
            Assert.Equal(400, menor, 3);

            // A peca de 400 mm sai da sobra do corte da peca de 800 mm.
            Assert.Equal(4, estoque.PlacasUsadas);

            List<PecaPlaca> reaproveitadas = face2.Pecas.Where(p => p.Reaproveitada).ToList();
            Assert.Single(reaproveitadas);

            PecaPlaca de800 = face2.Pecas.First(p => Math.Abs(p.Contorno.Largura - 800) < 1.0);
            Assert.Equal(de800.PlacaOrigem, reaproveitadas[0].PlacaOrigem);
        }

        [Fact]
        public void Deitada_ParedeDe4800x2700_UsaCincoPlacasEDuasReaproveitadas()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.Deitada, 4800, 2700), estoque);

            Assert.Equal(3, r.JuntasVerticaisPorFiada.Count);
            Assert.Equal(new List<double> { 2400 }, r.JuntasVerticaisPorFiada[0]);
            Assert.Equal(new List<double> { 1200, 3600 }, r.JuntasVerticaisPorFiada[1]);
            Assert.Equal(new List<double> { 2400 }, r.JuntasVerticaisPorFiada[2]);

            Assert.Equal(7, r.Pecas.Count);
            Assert.Equal(3, r.Pecas.Count(p => p.Inteira));

            Assert.Equal(5, estoque.PlacasUsadas);
            Assert.Equal(2, r.Pecas.Count(p => p.Reaproveitada));

            Assert.Contains(r.Avisos, a => a.Contains("travessa"));
        }

        [Fact]
        public void Porta_GeraRecortesEmL_ComJuntasAfastadasDasQuinasDoVao()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            var porta = new RetanguloMm(1000, 1900, 0, 2100);

            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 3600, 2700, 0, new List<RetanguloMm> { porta }),
                estoque);

            // Fiada de baixo com juntas em 400, 1600 e 2800; faixa de cima com 4 pecas.
            Assert.Equal(8, r.Pecas.Count);
            Assert.Equal(2, r.Pecas.Count(p => p.Recortes.Count == 1));

            // Nenhuma junta da fiada de baixo a menos de 30 cm das quinas da porta.
            foreach (double junta in r.JuntasVerticaisPorFiada[0])
            {
                Assert.True(Math.Abs(junta - 1000) >= 299.5);
                Assert.True(Math.Abs(junta - 1900) >= 299.5);
            }

            // A peca que comeca em 400 mm pega a quina esquerda da porta: recorte em L.
            PecaPlaca primeira = r.Pecas.First(
                p => p.Fiada == 1 && p.Recortes.Count == 1 && Math.Abs(p.Contorno.U0 - 400) < 1.0);
            Assert.Equal(600, primeira.Recortes[0].Largura, 1);
            Assert.Equal(2085, primeira.Recortes[0].Altura, 1);

            Assert.Contains(primeira.CodigoDoRecorte(0), new[] { primeira.Codigo + "-R1" });

            // O recorte que sobra da porta volta ao estoque e e usado nas pecas de cima.
            Assert.Contains(r.Pecas, p => p.Reaproveitada);
            Assert.DoesNotContain(r.Avisos, a => a.Contains("fora de montante"));
        }

        [Fact]
        public void AberturaQueCortaAPlacaAoMeio_GeraDuasPecasSoltas()
        {
            var estoque = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            var passagem = new RetanguloMm(500, 700, 0, 2400);

            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 1200, 2400, 0, new List<RetanguloMm> { passagem }),
                estoque);

            Assert.Equal(2, r.Pecas.Count);
            Assert.All(r.Pecas, p => Assert.Empty(p.Recortes));
            Assert.Empty(r.Juntas);
        }

        [Fact]
        public void Cimenticia_TemJuntaDe5mmEPlacasInteiras()
        {
            var estoque = new EstoquePlacas(TipoPlaca.Cimenticia, new MedidaPlaca(1200, 2400));

            // Parede de 2415: 15 mm de folga do piso + uma placa de 2400.
            ResultadoPaginacao r = Paginador.Paginar(
                Entrada(TipoPlaca.Cimenticia, OrientacaoPlaca.EmPe, 2400, 2415), estoque);

            Assert.Equal(2, r.Pecas.Count);
            Assert.All(r.Pecas, p => Assert.True(p.Inteira));

            Assert.Single(r.Juntas);
            Assert.True(Math.Abs(r.Juntas[0].ComprimentoMm - 2400) < 1.0);
            Assert.Equal(2, estoque.PlacasUsadas);
        }

        [Fact]
        public void JuntaNaDivisaEntrePaineisDeTiposDiferentes_EMista()
        {
            var estoqueGesso = new EstoquePlacas(TipoPlaca.GessoSt, new MedidaPlaca(1200, 2400));
            var estoqueGlasroc = new EstoquePlacas(TipoPlaca.GlasrocX, new MedidaPlaca(1200, 2400));

            ResultadoPaginacao esquerdo = Paginador.Paginar(
                Entrada(TipoPlaca.GessoSt, OrientacaoPlaca.EmPe, 1200, 2400), estoqueGesso);
            ResultadoPaginacao direito = Paginador.Paginar(
                Entrada(TipoPlaca.GlasrocX, OrientacaoPlaca.EmPe, 1200, 2400), estoqueGlasroc);

            List<JuntaPlaca> juntas = Paginador.JuntasDeDivisa(esquerdo, direito);

            Assert.Single(juntas);
            Assert.True(juntas[0].Mista);
            Assert.Equal(TipoPlaca.GessoSt, juntas[0].TipoA);
            Assert.Equal(TipoPlaca.GlasrocX, juntas[0].TipoB);
            Assert.True(juntas[0].ComprimentoMm > 2000);
        }

        [Fact]
        public void MenorDistancia_DevolveAMenorDiferenca()
        {
            double d = Paginador.MenorDistancia(new List<double> { 1200 }, new List<double> { 400, 1600 });
            Assert.Equal(400, d, 3);

            Assert.True(double.IsInfinity(Paginador.MenorDistancia(new List<double>(), new List<double> { 1 })));
        }
    }

    public class PlacaCatalogoTests
    {
        [Fact]
        public void MedidasDeFabrica_SeguemAsRegrasDoProjeto()
        {
            Assert.Single(PlacaCatalogo.MedidasDeFabrica(TipoPlaca.GlasrocX));
            Assert.Single(PlacaCatalogo.MedidasDeFabrica(TipoPlaca.Cimenticia));
            Assert.Single(PlacaCatalogo.MedidasDeFabrica(TipoPlaca.Osb));

            foreach (TipoPlaca gesso in new[]
            {
                TipoPlaca.GessoSt, TipoPlaca.GessoRu, TipoPlaca.GessoRf,
                TipoPlaca.GessoPerforma, TipoPlaca.PerformaRu
            })
            {
                Assert.Equal(2, PlacaCatalogo.MedidasDeFabrica(gesso).Count);
            }
        }

        [Fact]
        public void Cadastrar_AceitaNovaMedidaRecusaRepetidaEInvalida_ESalva()
        {
            string caminho = Path.Combine(Path.GetTempPath(), "lsf_placas_" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                var catalogo = new PlacaCatalogo(caminho);
                string erro;

                Assert.True(catalogo.Cadastrar(TipoPlaca.GessoSt, 1500, 1200, out erro));
                Assert.Equal(3, catalogo.MedidasDisponiveis(TipoPlaca.GessoSt).Count);

                Assert.False(catalogo.Cadastrar(TipoPlaca.GessoSt, 1200, 1500, out erro));
                Assert.NotNull(erro);

                Assert.False(catalogo.Cadastrar(TipoPlaca.GessoSt, 50, 1200, out erro));

                var recarregado = new PlacaCatalogo(caminho);
                Assert.Equal(3, recarregado.MedidasDisponiveis(TipoPlaca.GessoSt).Count);
                Assert.Contains(recarregado.MedidasDisponiveis(TipoPlaca.GessoSt), m => m.Personalizada);
            }
            finally
            {
                if (File.Exists(caminho)) File.Delete(caminho);
            }
        }
    }
}