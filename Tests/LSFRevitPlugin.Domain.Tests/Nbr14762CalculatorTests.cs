using Xunit;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Domain.Structural;

namespace LSFRevitPlugin.Domain.Tests
{
    public class Nbr14762CalculatorTests
    {
        private readonly Nbr14762Calculator _calc = new Nbr14762Calculator();

        // --- Flambagem Local ---

        [Fact]
        public void FlambagemLocal_PerfilRobusto_DeveAprovar()
        {
            var perfil = new PerfilLSF
            {
                Nome = "Ue 90x40 - 1.25mm",
                EspessuraMm = 1.25,
                LarguraMesaMm = 40,
                AreaMm2 = 200,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemLocal(perfil, cargaAxialN: 20000);

            Assert.True(resultado.Aprovado);
            Assert.Equal("Nenhum", resultado.ModoCritico);
        }

        [Fact]
        public void FlambagemLocal_PerfilFino_DeveReprovar()
        {
            var perfil = new PerfilLSF
            {
                Nome = "Ue 90x40 - 0.80mm (fino demais)",
                EspessuraMm = 0.80,
                LarguraMesaMm = 40,
                AreaMm2 = 200,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemLocal(perfil, cargaAxialN: 45000);

            Assert.False(resultado.Aprovado);
            Assert.Equal("Flambagem Local", resultado.ModoCritico);
        }

        // --- Flambagem Distorcional ---
        // Este e o modo que faltava no material original -- os numeros abaixo mostram
        // um caso onde o perfil passaria na verificacao local, mas reprova na distorcional,
        // exatamente o cenario que a NBR 14762 exige verificar e que estava sendo ignorado.

        [Fact]
        public void FlambagemDistorcional_CargaBaixa_DeveAprovar()
        {
            var perfil = new PerfilLSF
            {
                EspessuraMm = 1.25,
                AlturaAlmaMm = 90,
                AreaMm2 = 200,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemDistorcional(perfil, cargaAxialN: 5000);

            Assert.True(resultado.Aprovado);
        }

        [Fact]
        public void FlambagemDistorcional_CargaAlta_DeveReprovar()
        {
            var perfil = new PerfilLSF
            {
                EspessuraMm = 1.25,
                AlturaAlmaMm = 90,
                AreaMm2 = 200,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemDistorcional(perfil, cargaAxialN: 12000);

            Assert.False(resultado.Aprovado);
            Assert.Equal("Flambagem Distorcional", resultado.ModoCritico);
        }

        // --- Flambagem Global (flexo-compressao) ---

        [Fact]
        public void FlambagemGlobal_CargaModerada_DeveAprovar()
        {
            var perfil = new PerfilLSF
            {
                AlturaAlmaMm = 90,
                AreaMm2 = 200,
                MomentoInerciaMm4 = 150000,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemGlobal(
                perfil, comprimentoDestravadoMm: 3000, cargaAxialN: 10000, momentoFletorNmm: 200000);

            Assert.True(resultado.Aprovado);
        }

        [Fact]
        public void FlambagemGlobal_CargaAlta_DeveReprovar()
        {
            var perfil = new PerfilLSF
            {
                AlturaAlmaMm = 90,
                AreaMm2 = 200,
                MomentoInerciaMm4 = 150000,
                TensaoEscoamentoMPa = 250
            };

            var resultado = _calc.VerificarFlambagemGlobal(
                perfil, comprimentoDestravadoMm: 3000, cargaAxialN: 25000, momentoFletorNmm: 500000);

            Assert.False(resultado.Aprovado);
            Assert.Equal("Flambagem Global (flexo-compressao)", resultado.ModoCritico);
        }

        // --- Flecha (L/300) ---

        [Fact]
        public void Flecha_DentroDoLimite_DeveAprovar()
        {
            var resultado = _calc.VerificarFlecha(comprimentoMm: 3000, flechaCalculadaMm: 8);

            Assert.True(resultado.Aprovado);
        }

        [Fact]
        public void Flecha_AcimaDoLimite_DeveReprovar()
        {
            var resultado = _calc.VerificarFlecha(comprimentoMm: 3000, flechaCalculadaMm: 15);

            Assert.False(resultado.Aprovado);
            Assert.Equal("Flecha excedida (L/300)", resultado.ModoCritico);
        }
    }
}
