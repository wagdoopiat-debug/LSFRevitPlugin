using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes do parcelamento das placas de cobertura: convexidade, divisao em chapas com juntas,
    /// recorte pelo contorno e parcelamento tambem de contorno nao convexo (triangulacao + costura).
    /// </summary>
    public class LajePaginacaoTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        private static ContornoLaje FormaL()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0), new Ponto2DMm(6000, 2000),
                new Ponto2DMm(2000, 2000), new Ponto2DMm(2000, 6000), new Ponto2DMm(0, 6000)
            });
        }

        private static double AreaContorno(IReadOnlyList<Ponto2DMm> pts)
        {
            double soma = 0.0;
            for (int i = 0; i < pts.Count; i++)
            {
                Ponto2DMm a = pts[i];
                Ponto2DMm b = pts[(i + 1) % pts.Count];
                soma += a.XMm * b.YMm - b.XMm * a.YMm;
            }

            return System.Math.Abs(soma) / 2.0;
        }

        [Fact]
        public void Retangulo_e_convexo_e_forma_l_nao()
        {
            Assert.True(PaginacaoPlacas.EhConvexo(Retangulo()));
            Assert.False(PaginacaoPlacas.EhConvexo(FormaL()));
        }

        [Fact]
        public void Retangulo_parcela_em_varias_chapas()
        {
            ResultadoPaginacao r = PaginacaoPlacas.Calcular(
                Retangulo(), OrientacaoVigas.Calcular(Retangulo(), null),
                PaginacaoPlacas.LarguraOSBMm, PaginacaoPlacas.ComprimentoOSBMm,
                0.0, 18.0, PaginacaoPlacas.JuntaPadraoMm, 0.0, PaginacaoPlacas.LarguraOSBMm / 2.0);

            Assert.True(r.Paginou);
            Assert.True(r.Placas.Count > 1);
        }

        [Fact]
        public void Soma_das_chapas_nao_ultrapassa_a_area_da_laje()
        {
            const double areaLaje = 6000.0 * 4000.0;

            ResultadoPaginacao r = PaginacaoPlacas.Calcular(
                Retangulo(), OrientacaoVigas.Calcular(Retangulo(), null),
                PaginacaoPlacas.LarguraOSBMm, PaginacaoPlacas.ComprimentoOSBMm,
                0.0, 18.0, 0.0, 0.0, 0.0);

            double soma = r.Placas.Sum(p => AreaContorno(p.ContornoMundo));

            Assert.True(soma <= areaLaje + 1.0);
            Assert.True(soma > areaLaje * 0.95);
        }

        [Fact]
        public void Juntas_reduzem_a_area_coberta()
        {
            OrientacaoVigas o = OrientacaoVigas.Calcular(Retangulo(), null);

            ResultadoPaginacao semJunta = PaginacaoPlacas.Calcular(
                Retangulo(), o, 1200.0, 2400.0, 0.0, 18.0, 0.0, 0.0, 0.0);
            ResultadoPaginacao comJunta = PaginacaoPlacas.Calcular(
                Retangulo(), o, 1200.0, 2400.0, 0.0, 18.0, 20.0, 0.0, 0.0);

            double areaSem = semJunta.Placas.Sum(p => AreaContorno(p.ContornoMundo));
            double areaCom = comJunta.Placas.Sum(p => AreaContorno(p.ContornoMundo));

            Assert.True(areaCom < areaSem);
        }

        [Fact]
        public void Contorno_nao_convexo_tambem_e_parcelado()
        {
            ResultadoPaginacao r = PaginacaoPlacas.Calcular(
                FormaL(), OrientacaoVigas.Calcular(FormaL(), null),
                PaginacaoPlacas.LarguraOSBMm, PaginacaoPlacas.ComprimentoOSBMm,
                0.0, 18.0, 3.0, 0.0, 600.0);

            Assert.True(r.Paginou);
            Assert.True(r.Placas.Count > 1);
            Assert.NotEmpty(r.Avisos);
        }

        [Fact]
        public void Clipes_ficam_dentro_da_caixa_do_contorno()
        {
            ResultadoPaginacao r = PaginacaoPlacas.Calcular(
                Retangulo(), OrientacaoVigas.Calcular(Retangulo(), null),
                1200.0, 2400.0, 0.0, 18.0, 3.0, 0.0, 600.0);

            foreach (PlacaPaginada placa in r.Placas)
            {
                foreach (Ponto2DMm p in placa.ContornoMundo)
                {
                    Assert.InRange(p.XMm, -1.0, 6001.0);
                    Assert.InRange(p.YMm, -1.0, 4001.0);
                }
            }
        }

        [Fact]
        public void Chapas_preservam_cota_e_espessura_da_camada()
        {
            ResultadoPaginacao r = PaginacaoPlacas.Calcular(
                Retangulo(), OrientacaoVigas.Calcular(Retangulo(), null),
                1200.0, 2400.0, 100.0, 18.0, 3.0, 0.0, 600.0);

            Assert.All(r.Placas, p =>
            {
                Assert.Equal(100.0, p.BaseMm, 6);
                Assert.Equal(18.0, p.EspessuraMm, 6);
            });
        }

        [Fact]
        public void Junta_obrigatoria_e_fixa_em_3mm()
        {
            // Regra fixa: a junta entre placas nao e configuravel e vale 3 mm.
            Assert.Equal(3.0, PaginacaoPlacas.JuntaPadraoMm, 6);
        }

        [Fact]
        public void Osb_e_cimenticia_usam_a_mesma_orientacao_sem_rotacao_relativa()
        {
            PaginacaoPlacas.DimensoesOrientadas(osb: true, out double osbU, out double osbV);
            PaginacaoPlacas.DimensoesOrientadas(osb: false, out double cimU, out double cimV);

            Assert.Equal(osbU, cimU, 6);
            Assert.Equal(osbV, cimV, 6);
            Assert.Equal(PaginacaoPlacas.LarguraOSBMm, osbU, 6);
            Assert.Equal(PaginacaoPlacas.ComprimentoOSBMm, osbV, 6);
        }
    }
}
