using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeParametrosConfigTests
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
        public void Sistema_padrao_e_implementado()
        {
            var p = new ParametrosLaje { Contorno = Retangulo() };

            Assert.Equal(SistemaLaje.VigasLsfComGuiasBorda, p.Sistema);
            Assert.Empty(p.Validar());
        }

        [Fact]
        public void Sistema_nao_implementado_gera_erro()
        {
            var p = new ParametrosLaje { Contorno = Retangulo(), Sistema = (SistemaLaje)999 };

            Assert.Contains(p.Validar(), e => e.Contains("Sistema de laje nao implementado"));
        }

        [Fact]
        public void Modo_manual_sem_angulo_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                ModoOrientacao = OrientacaoVigasModo.Manual,
                DirecaoVigasGraus = null
            };

            Assert.Contains(p.Validar(), e => e.Contains("manual"));
        }

        [Fact]
        public void Modo_paralela_com_aresta_invalida_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                ModoOrientacao = OrientacaoVigasModo.ParalelaAresta,
                ArestaReferencia = 10
            };

            Assert.Contains(p.Validar(), e => e.Contains("invalida"));
        }

        [Fact]
        public void Direcao_efetiva_resolve_modo_paralela()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                ModoOrientacao = OrientacaoVigasModo.ParalelaAresta,
                ArestaReferencia = 1 // aresta vertical -> 90 graus
            };

            System.Collections.Generic.List<string> erros;
            double? d = p.DirecaoEfetivaGraus(out erros);

            Assert.Empty(erros);
            Assert.Equal(90.0, d.GetValueOrDefault(), 6);
        }

        [Fact]
        public void Direcao_efetiva_automatica_devolve_nulo()
        {
            var p = new ParametrosLaje { Contorno = Retangulo() };

            System.Collections.Generic.List<string> erros;
            double? d = p.DirecaoEfetivaGraus(out erros);

            Assert.Empty(erros);
            Assert.Null(d);
        }
    }
}
