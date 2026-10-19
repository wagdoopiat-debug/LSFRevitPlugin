using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Composicao padrao do piso seco: placa unica sobre as vigas. O tipo real da placa e escolhido
    /// no catalogo de placas existente (tarefa da laje seca); aqui ficam so os padroes de peso.
    /// </summary>
    public class PisoSecoSpec
    {
        public string TipoPlaca { get; set; } = "OSB";

        public ValorCatalogo NumeroCamadas { get; set; } = new ValorCatalogo(
            "NumeroCamadas", 1.0, "un", OrigemValor.Provisorio, "Padrao provisorio: uma camada de placa");

        public ValorCatalogo EspessuraMm { get; set; } = new ValorCatalogo(
            "EspessuraMm", 18.0, "mm", OrigemValor.Provisorio, "Padrao provisorio: OSB 18 mm");

        public ValorCatalogo MassaEspecificaKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaKgM3", 650.0, "kg/m3", OrigemValor.Provisorio,
            "Padrao provisorio: OSB (referencia usual 600 a 650 kg/m3)");

        /// <summary>Peso por m2 = camadas x espessura (m) x massa especifica.</summary>
        public ValorCatalogo PesoKgM2 => new ValorCatalogo(
            "PesoKgM2",
            NumeroCamadas.Valor * (EspessuraMm.Valor / 1000.0) * MassaEspecificaKgM3.Valor,
            "kg/m2", OrigemValor.Calculado,
            "NumeroCamadas x EspessuraMm / 1000 x MassaEspecificaKgM3");

        public static PisoSecoSpec CriarPadrao()
        {
            return new PisoSecoSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { NumeroCamadas, EspessuraMm, MassaEspecificaKgM3, PesoKgM2 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("PisoSecoSpec", Valores());
        }
    }
}
