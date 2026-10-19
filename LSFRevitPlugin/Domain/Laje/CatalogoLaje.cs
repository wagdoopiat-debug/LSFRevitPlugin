using System.Collections.Generic;
using System.Linq;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Ponto unico de acesso aos catalogos da laje (telha, argamassa, tela, piso seco e criterios).
    /// Nenhum valor fica em branco: cada um tem origem e fonte. PendenciasDoCatalogo lista o que
    /// ainda nao esta Conferido, para o usuario ver o que depende de conferencia.
    /// </summary>
    public class CatalogoLaje
    {
        public TelhaTp40 Telha { get; set; } = TelhaTp40.CriarPadrao();

        public ArgamassaSpec Argamassa { get; set; } = ArgamassaSpec.CriarPadrao();

        public TelaSoldadaSpec Tela { get; set; } = TelaSoldadaSpec.CriarPadrao();

        public PisoSecoSpec PisoSeco { get; set; } = PisoSecoSpec.CriarPadrao();

        public CriteriosLaje Criterios { get; set; } = CriteriosLaje.CriarPadrao();

        public static CatalogoLaje CriarPadrao()
        {
            return new CatalogoLaje();
        }

        /// <summary>Todos os valores de todos os grupos, inclusive os calculados.</summary>
        public IEnumerable<ValorCatalogo> TodosOsValores()
        {
            return Telha.Valores()
                .Concat(Argamassa.Valores())
                .Concat(Tela.Valores())
                .Concat(PisoSeco.Valores())
                .Concat(Criterios.Valores());
        }

        /// <summary>Todo valor cuja origem nao e Conferido, com o grupo a que pertence.</summary>
        public List<PendenciaCatalogo> PendenciasDoCatalogo()
        {
            var lista = new List<PendenciaCatalogo>();
            lista.AddRange(Telha.Pendencias());
            lista.AddRange(Argamassa.Pendencias());
            lista.AddRange(Tela.Pendencias());
            lista.AddRange(PisoSeco.Pendencias());
            lista.AddRange(Criterios.Pendencias());
            return lista;
        }

        /// <summary>Erros de coerencia dos catalogos (lista vazia = coerente).</summary>
        public List<string> Validar()
        {
            var erros = new List<string>();
            erros.AddRange(Telha.Validar());
            erros.AddRange(Tela.Validar());
            return erros;
        }
    }
}
