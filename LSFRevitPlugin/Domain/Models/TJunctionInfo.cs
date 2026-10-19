using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Representa um encontro em T: a ponta da parede selecionada cai no MEIO de outra
    /// parede (a "passante"), nao na ponta dela.
    /// </summary>
    public class TJunctionInfo
    {
        public XYZ PontoT { get; set; }
        public ElementId ParedeAtravessadaId { get; set; }
        public XYZ DirecaoPropria { get; set; }
        public XYZ DirecaoPassante { get; set; }

        /// <summary>
        /// Espessura real do nucleo estrutural da parede PASSANTE (lida do modelo via
        /// CompoundStructure), usada para alinhar os montantes laterais do Triplo com a
        /// espessura de verdade da parede, nao um valor fixo de perfil.
        /// </summary>
        public double EspessuraParedePassanteInterna { get; set; }
    }
}
