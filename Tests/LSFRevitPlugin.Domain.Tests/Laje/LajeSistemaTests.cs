using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeSistemaTests
    {
        [Fact]
        public void Catalogo_tem_somente_um_sistema_e_ele_esta_implementado()
        {
            Assert.Single(CatalogoSistemasLaje.Todos);
            Assert.True(CatalogoSistemasLaje.VigasLsfComGuiasBorda.Implementado);
        }

        [Fact]
        public void Obter_devolve_o_sistema_implementado()
        {
            DescricaoSistemaLaje d = CatalogoSistemasLaje.Obter(SistemaLaje.VigasLsfComGuiasBorda);

            Assert.NotNull(d);
            Assert.Equal(SistemaLaje.VigasLsfComGuiasBorda, d.Sistema);
            Assert.True(d.Implementado);
            Assert.False(string.IsNullOrWhiteSpace(d.Nome));
            Assert.False(string.IsNullOrWhiteSpace(d.Descricao));
        }

        [Fact]
        public void Nenhum_sistema_do_catalogo_fica_sem_descricao()
        {
            Assert.All(CatalogoSistemasLaje.Todos, d =>
            {
                Assert.False(string.IsNullOrWhiteSpace(d.Nome));
                Assert.False(string.IsNullOrWhiteSpace(d.Descricao));
                Assert.NotNull(CatalogoSistemasLaje.Obter(d.Sistema));
            });
        }

        [Fact]
        public void Obter_de_enum_invalido_devolve_nulo()
        {
            Assert.Null(CatalogoSistemasLaje.Obter((SistemaLaje)999));
        }
    }
}
