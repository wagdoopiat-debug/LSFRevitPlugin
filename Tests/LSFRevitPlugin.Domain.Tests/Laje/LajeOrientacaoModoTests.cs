using System.Collections.Generic;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeOrientacaoModoTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        [Fact]
        public void Automatica_devolve_nulo_sem_erros()
        {
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.Automatica, 0, null, out erros);

            Assert.Null(d);
            Assert.Empty(erros);
        }

        [Fact]
        public void Manual_normaliza_o_angulo_para_o_intervalo_0_180()
        {
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.Manual, 0, 200.0, out erros);

            Assert.Empty(erros);
            Assert.Equal(20.0, d.GetValueOrDefault(), 6);
        }

        [Fact]
        public void Manual_sem_angulo_gera_erro()
        {
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.Manual, 0, null, out erros);

            Assert.Null(d);
            Assert.NotEmpty(erros);
        }

        [Fact]
        public void Paralela_a_aresta_usa_o_angulo_da_aresta()
        {
            // Aresta 0 do retangulo: (0,0) -> (6000,0), angulo 0.
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.ParalelaAresta, 0, null, out erros);

            Assert.Empty(erros);
            Assert.Equal(0.0, d.GetValueOrDefault(), 6);

            // Aresta 1: (6000,0) -> (6000,4000), angulo 90.
            double? d1 = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.ParalelaAresta, 1, null, out erros);

            Assert.Empty(erros);
            Assert.Equal(90.0, d1.GetValueOrDefault(), 6);
        }

        [Fact]
        public void Perpendicular_a_aresta_soma_90_graus()
        {
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.PerpendicularAresta, 0, null, out erros);

            Assert.Empty(erros);
            Assert.Equal(90.0, d.GetValueOrDefault(), 6);
        }

        [Fact]
        public void Aresta_fora_do_intervalo_gera_erro()
        {
            List<string> erros;
            double? d = ResolvedorOrientacaoVigas.DirecaoGraus(
                Retangulo(), OrientacaoVigasModo.ParalelaAresta, 9, null, out erros);

            Assert.Null(d);
            Assert.Contains(erros, e => e.Contains("invalida"));
        }

        [Fact]
        public void Normalizar180_trata_negativos_e_multiplos()
        {
            Assert.Equal(0.0, ResolvedorOrientacaoVigas.Normalizar180(-180.0), 6);
            Assert.Equal(90.0, ResolvedorOrientacaoVigas.Normalizar180(-270.0), 6);
            Assert.Equal(45.0, ResolvedorOrientacaoVigas.Normalizar180(405.0), 6);
        }
    }
}
