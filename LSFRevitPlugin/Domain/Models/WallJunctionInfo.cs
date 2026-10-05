using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Representa um no (encontro) detectado entre a parede selecionada e outra parede,
    /// em uma das extremidades do eixo.
    /// </summary>
    public class WallJunctionInfo
    {
        public XYZ PontoEncontro { get; set; }
        public ElementId ParedeConectadaId { get; set; }
        public double AnguloGraus { get; set; }
        public XYZ DirecaoPropria { get; set; }
        public XYZ DirecaoConectada { get; set; }

        /// <summary>
        /// Espessura real da parede conectada, em unidades internas do Revit (pes) -- lida
        /// diretamente do modelo (Wall.Width), nao um valor fixo. Usada para deslocar o reforco
        /// de canto ate o canto fisico real (nao so ate o cruzamento dos eixos).
        /// </summary>
        public double EspessuraParedeConectadaInterna { get; set; }

        public bool EhAnguloReto => System.Math.Abs(AnguloGraus - 90.0) < 1.0;
    }
}
