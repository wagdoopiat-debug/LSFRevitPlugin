using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Representa um encontro em cruz (+): a parede selecionada e outra parede se cruzam
    /// pelo MEIO das duas, nenhuma das duas termina no ponto de encontro.
    /// </summary>
    public class CruzJunctionInfo
    {
        public XYZ PontoCruz { get; set; }
        public ElementId ParedeCruzadaId { get; set; }
        public XYZ DirecaoPropria { get; set; }
        public XYZ DirecaoCruzada { get; set; }
        public double EspessuraParedeCruzadaInterna { get; set; }

        /// <summary>Espessura do nucleo da propria parede selecionada (nao a cruzada).</summary>
        public double EspessuraParedePropriaInterna { get; set; }
    }
}
