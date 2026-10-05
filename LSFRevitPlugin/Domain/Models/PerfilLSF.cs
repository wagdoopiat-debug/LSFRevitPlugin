namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Representa as propriedades geometricas de um perfil de aco formado a frio (U enrijecido / Ue),
    /// independente do Revit -- estes valores podem vir de uma tabela de fabricante ou ser calculados
    /// a partir da secao nominal.
    /// </summary>
    public class PerfilLSF
    {
        /// <summary>Nome comercial do perfil, ex: "Ue 90x40x12 - 1.25mm"</summary>
        public string Nome { get; set; }

        /// <summary>Espessura nominal da chapa, em milimetros.</summary>
        public double EspessuraMm { get; set; }

        /// <summary>Altura da alma (h), em milimetros.</summary>
        public double AlturaAlmaMm { get; set; }

        /// <summary>Largura da mesa (b), em milimetros.</summary>
        public double LarguraMesaMm { get; set; }

        /// <summary>Largura do enrijecedor de borda (d), em milimetros. Zero se o perfil nao tiver enrijecedor.</summary>
        public double LarguraEnrijecedorMm { get; set; }

        /// <summary>Area bruta da secao transversal, em mm2.</summary>
        public double AreaMm2 { get; set; }

        /// <summary>Momento de inercia em relacao ao eixo de maior resistencia, em mm4.</summary>
        public double MomentoInerciaMm4 { get; set; }

        /// <summary>Modulo de elasticidade do aco, em MPa (N/mm2). Padrao 200000 MPa para aco estrutural.</summary>
        public double ModuloElasticidadeMPa { get; set; } = 200000.0;

        /// <summary>Tensao de escoamento do aco, em MPa. Padrao 250 MPa (aco ZAR 250 / equivalente), CONFIRME com o fabricante.</summary>
        public double TensaoEscoamentoMPa { get; set; } = 250.0;
    }
}
