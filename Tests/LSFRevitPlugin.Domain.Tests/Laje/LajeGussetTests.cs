using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeGussetTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(4000, 0),
                new Ponto2DMm(4000, 3000), new Ponto2DMm(0, 3000)
            });
        }

        [Fact]
        public void Gusset_ocorre_em_nos_com_conexao_da_trelica_Warren()
        {
            var membros = ComposicaoTrelica.Membros(TrelicaTipo.Warren, 4000.0, 200.0, 8);
            var gussets = GussetLaje.Calcular(membros, 4000.0, 200.0, 80.0, 1.5);
            Assert.True(gussets.Count >= 9);
        }

        [Fact]
        public void Gusset_encerra_sem_erro_quando_sem_membros()
        {
            var gussets = GussetLaje.Calcular(new List<MembroTrelica>(), 3000.0, 200.0);
            Assert.Empty(gussets);
        }
    }
}
