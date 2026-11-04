using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeExclusaoTests
    {
        private static ElementoLajeIdentificado Elemento(
            int id, int lajeId, PapelElementoLaje papel = PapelElementoLaje.Viga)
        {
            return new ElementoLajeIdentificado
            {
                ElementId = id,
                Identidade = new IdentidadeLaje
                {
                    LajeId = lajeId,
                    Tipo = TipoLaje.Seca,
                    Papel = papel,
                    Numero = id
                }
            };
        }

        [Fact]
        public void Exclusao_individual_seleciona_somente_a_laje_pedida()
        {
            var elementos = new List<ElementoLajeIdentificado>
            {
                Elemento(1, 1),
                Elemento(2, 1, PapelElementoLaje.Travamento),
                Elemento(3, 2),
                Elemento(4, 2, PapelElementoLaje.Fita)
            };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Individual(1, elementos);

            Assert.Equal(new[] { 1, 2 }, plano.ElementosIds);
            Assert.Equal(new[] { 1 }, plano.LajesIds);
            Assert.True(plano.TemAlvo);
            Assert.Empty(plano.Avisos);
        }

        [Fact]
        public void Exclusao_total_seleciona_todas_as_lajes_identificadas()
        {
            var elementos = new List<ElementoLajeIdentificado>
            {
                Elemento(1, 1),
                Elemento(2, 2),
                Elemento(3, 3)
            };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Total(elementos);

            Assert.Equal(new[] { 1, 2, 3 }, plano.ElementosIds);
            Assert.Equal(new[] { 1, 2, 3 }, plano.LajesIds);
            Assert.True(plano.Total);
            Assert.True(plano.TemAlvo);
        }

        [Fact]
        public void Exclusao_ignora_entradas_invalidas_e_duplicadas()
        {
            var elementos = new List<ElementoLajeIdentificado>
            {
                new ElementoLajeIdentificado { ElementId = 0, Identidade = new IdentidadeLaje { LajeId = 1 } },
                new ElementoLajeIdentificado { ElementId = 9 },
                Elemento(5, 1),
                Elemento(5, 1)
            };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Individual(1, elementos);

            Assert.Equal(new[] { 5 }, plano.ElementosIds);
            Assert.Empty(plano.Avisos);
        }

        [Fact]
        public void Exclusao_de_laje_inexistente_nao_tem_alvo()
        {
            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Individual(
                99, new[] { Elemento(1, 1) });

            Assert.False(plano.TemAlvo);
            Assert.Single(plano.Avisos);
        }

        [Fact]
        public void Exclusao_total_sem_elementos_nao_tem_alvo()
        {
            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Total(
                Enumerable.Empty<ElementoLajeIdentificado>());

            Assert.False(plano.TemAlvo);
            Assert.Empty(plano.LajesIds);
            Assert.Single(plano.Avisos);
        }

        [Fact]
        public void Identidades_duplicadas_geram_aviso_e_sao_removidas_uma_vez()
        {
            var elementos = new List<ElementoLajeIdentificado>
            {
                new ElementoLajeIdentificado
                {
                    ElementId = 7,
                    Identidade = new IdentidadeLaje
                    {
                        LajeId = 1, Tipo = TipoLaje.Seca,
                        Papel = PapelElementoLaje.Viga, Numero = 1
                    }
                },
                new ElementoLajeIdentificado
                {
                    ElementId = 8,
                    Identidade = new IdentidadeLaje
                    {
                        LajeId = 1, Tipo = TipoLaje.Seca,
                        Papel = PapelElementoLaje.Viga, Numero = 1
                    }
                }
            };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Total(elementos);

            Assert.Equal(new[] { 7, 8 }, plano.ElementosIds);
            Assert.Single(plano.Avisos);
        }
    }
}
