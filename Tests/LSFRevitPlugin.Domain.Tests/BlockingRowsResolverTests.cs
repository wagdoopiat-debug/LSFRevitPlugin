using System.Collections.Generic;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests
{
    public class BlockingRowsResolverTests
    {
        private const double AlturaParedeCm = 240.0;
        private const double FolgaGuiasCm = 0.5;

        private static List<double> Resolver(BlockingConfig config, List<string> avisos)
        {
            return BlockingRowsResolver.ResolverAlturasRelativasCm(
                config, AlturaParedeCm, FolgaGuiasCm, avisos);
        }

        [Fact]
        public void Desabilitado_RetornaVazio()
        {
            var avisos = new List<string>();
            var alturas = Resolver(new BlockingConfig { Habilitado = false, Fileiras = 3 }, avisos);
            Assert.Empty(alturas);
        }

        [Fact]
        public void AlturasExplicitas_SaoUsadas_IndependenteDeFileiras()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 0,
                AlturasRelativasCm = new List<double> { 200, 100 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Equal(2, alturas.Count);
            Assert.Equal(100.0, alturas[0], 3);
            Assert.Equal(200.0, alturas[1], 3);
            Assert.Empty(avisos);
        }

        [Fact]
        public void AlturasExplicitas_TemPrioridadeSobreJuntas()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 1,
                AlinharJuntasPlacas = true,
                AlturasRelativasCm = new List<double> { 110 },
                AlturasJuntasPlacasCm = new List<double> { 180 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Single(alturas);
            Assert.Equal(110.0, alturas[0], 3);
        }

        [Fact]
        public void AlinharJuntas_ComDadosUsaAsJuntas()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 0,
                AlinharJuntasPlacas = true,
                AlturasJuntasPlacasCm = new List<double> { 120, 180 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Equal(2, alturas.Count);
            Assert.Equal(120.0, alturas[0], 3);
            Assert.Equal(180.0, alturas[1], 3);
        }

        [Fact]
        public void AlinharJuntas_SemDados_AvisaECaiParaFileiras()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 2,
                AlinharJuntasPlacas = true,
                AlturasJuntasPlacasCm = new List<double>()
            };

            var alturas = Resolver(config, avisos);

            Assert.Equal(2, alturas.Count);
            Assert.Contains(avisos, a => a.Contains("não há juntas de placas"));
        }

        [Fact]
        public void FileirasAutomaticas_DistribuidasNosLimitesUteis()
        {
            var avisos = new List<string>();
            var alturas = Resolver(new BlockingConfig { Habilitado = true, Fileiras = 3 }, avisos);

            Assert.Equal(3, alturas.Count);
            foreach (double h in alturas)
            {
                Assert.True(h > FolgaGuiasCm && h < AlturaParedeCm - FolgaGuiasCm);
            }
            Assert.True(alturas[0] < alturas[1] && alturas[1] < alturas[2]);
        }

        [Fact]
        public void AlturaForaDosLimites_EIgnoradaComAviso()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 0,
                AlturasRelativasCm = new List<double> { 100, 300 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Single(alturas);
            Assert.Equal(100.0, alturas[0], 3);
            Assert.Contains(avisos, a => a.Contains("fora dos limites"));
        }

        [Fact]
        public void AlturaDuplicada_EIgnoradaComAviso()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 0,
                AlturasRelativasCm = new List<double> { 100, 100.05 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Single(alturas);
            Assert.Contains(avisos, a => a.Contains("duplicada"));
        }

        [Fact]
        public void JuntasComDuplicadaEForaDosLimites_SaoFiltradas()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig
            {
                Habilitado = true,
                Fileiras = 0,
                AlinharJuntasPlacas = true,
                AlturasJuntasPlacasCm = new List<double> { 120, 120.05, 300 }
            };

            var alturas = Resolver(config, avisos);

            Assert.Single(alturas);
            Assert.Equal(120.0, alturas[0], 3);
        }

        [Fact]
        public void SemFileirasSemAlturasSemJuntas_RetornaVazio()
        {
            var avisos = new List<string>();
            var config = new BlockingConfig { Habilitado = true, Fileiras = 0 };

            var alturas = Resolver(config, avisos);

            Assert.Empty(alturas);
        }
    }
}
