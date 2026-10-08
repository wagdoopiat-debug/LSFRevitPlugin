using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Boards;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>Uma placa ja gerada no projeto, lida do texto do Comentario dela.</summary>
    public sealed class PlacaExistente
    {
        public DirectShape Elemento { get; set; }
        public string Codigo { get; set; }
        public int ParedeId { get; set; }
        public int Painel { get; set; }
        public int Face { get; set; }
        public TipoPlaca? Tipo { get; set; }
        public MedidaPlaca Medida { get; set; }
        public OrientacaoPlaca? Orientacao { get; set; }
        public string PlacaOrigem { get; set; } = "";
        public bool Reaproveitada { get; set; }
        public double AreaLiquidaMm2 { get; set; }
    }

    /// <summary>
    /// As placas que ja existem no projeto. Serve para mostrar na janela o que cada painel usa
    /// hoje e para refazer so os paineis marcados.
    /// </summary>
    public sealed class PlacasExistentes
    {
        private readonly List<PlacaExistente> _todas = new List<PlacaExistente>();

        public IReadOnlyList<PlacaExistente> Todas { get { return _todas; } }

        public static PlacasExistentes Ler(Document doc)
        {
            var resultado = new PlacasExistentes();

            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                PlacaExistente placa = Interpretar(ds);
                if (placa != null) resultado._todas.Add(placa);
            }

            return resultado;
        }

        public List<PlacaExistente> DoPainel(int paredeId, int painel)
        {
            return _todas.Where(p => p.ParedeId == paredeId && p.Painel == painel).ToList();
        }

        public bool TemPlacas(int paredeId, int painel)
        {
            return _todas.Any(p => p.ParedeId == paredeId && p.Painel == painel);
        }

        /// <summary>
        /// Tipo, medida e orientacao que uma face de um painel usa hoje. Nulo se nao ha placas
        /// ou se elas foram geradas numa versao que nao gravava esses dados.
        /// </summary>
        public EscolhaPlaca Escolha(int paredeId, int painel, int face)
        {
            PlacaExistente p = _todas.FirstOrDefault(x =>
                x.ParedeId == paredeId && x.Painel == painel && x.Face == face
                && x.Tipo.HasValue && x.Medida != null && x.Orientacao.HasValue);

            if (p == null) return null;

            return new EscolhaPlaca
            {
                Tipo = p.Tipo.Value,
                Medida = p.Medida,
                Orientacao = p.Orientacao.Value
            };
        }

        private static PlacaExistente Interpretar(DirectShape ds)
        {
            string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();

            // Tentar ler identidade via ES primeiro
            bool temIdentidadeEs = false;
            int paredeEs = 0;
            int painelEs = 0;
            int faceEs = 0;
            string tipoEs = string.Empty;
            string orientacaoEs = string.Empty;

            try
            {
                if (PertencenciaPlacaService.TentarLer(ds, out IdentidadePlaca identEs) && identEs != null)
                {
                    temIdentidadeEs = true;
                    paredeEs = identEs.ParedeId;
                    painelEs = identEs.NumeroPainel;
                    faceEs = identEs.Face;
                    tipoEs = identEs.TipoPlaca ?? string.Empty;
                    orientacaoEs = identEs.Orientacao ?? string.Empty;
                }
            }
            catch
            {
                temIdentidadeEs = false;
            }

            if (!temIdentidadeEs)
            {
                if (comentario == null
                    || !comentario.StartsWith(PlacasService.PrefixoEtiqueta, StringComparison.Ordinal))
                {
                    return null;
                }

                string[] seg = comentario.Split(new[] { " | " }, StringSplitOptions.None);
                if (seg.Length < 7) return null;

                int parede;
                int painel;
                int face;
                if (!Numero(seg[1], "Parede ", out parede)
                    || !Numero(seg[2], "Painel ", out painel)
                    || !Numero(seg[3], "Face ", out face))
                {
                    return null;
                }

                var placa = new PlacaExistente
                {
                    Elemento = ds,
                    Codigo = seg[5],
                    ParedeId = parede,
                    Painel = painel,
                    Face = face
                };

                foreach (TipoPlaca t in PlacaCatalogo.TodosOsTipos())
                {
                    if (PlacaCatalogo.NomeDoTipo(t) == seg[4]) placa.Tipo = t;
                }

                for (int i = 6; i < seg.Length; i++)
                {
                    string s = seg[i];

                    if (s.StartsWith("placa ", StringComparison.Ordinal))
                    {
                        string resto = s.Substring("placa ".Length);
                        int sobra = resto.IndexOf(" (sobra de", StringComparison.Ordinal);
                        placa.Reaproveitada = sobra >= 0;
                        placa.PlacaOrigem = sobra >= 0 ? resto.Substring(0, sobra) : resto;
                    }
                    else if (s.StartsWith("medida ", StringComparison.Ordinal))
                    {
                        string[] lados = s.Substring("medida ".Length).Split('x');
                        double a;
                        double b;
                        if (lados.Length == 2 && Decimal(lados[0], out a) && Decimal(lados[1], out b))
                        {
                            placa.Medida = new MedidaPlaca(a, b);
                        }
                    }
                    else if (s.StartsWith("orientacao ", StringComparison.Ordinal))
                    {
                        OrientacaoPlaca o;
                        if (Enum.TryParse(s.Substring("orientacao ".Length), out o)) placa.Orientacao = o;
                    }
                    else if (s.StartsWith("area ", StringComparison.Ordinal))
                    {
                        double area;
                        if (Decimal(s.Substring("area ".Length), out area)) placa.AreaLiquidaMm2 = area;
                    }
                }

                return placa;
            }
            else
            {
                // ES presente: usar identidade do ES, metadados do Comments (se válido)
                var placaEs = new PlacaExistente
                {
                    Elemento = ds,
                    ParedeId = paredeEs,
                    Painel = painelEs,
                    Face = faceEs
                };

                // Mapear TipoPlaca por nome
                foreach (TipoPlaca t in PlacaCatalogo.TodosOsTipos())
                {
                    if (PlacaCatalogo.NomeDoTipo(t) == tipoEs)
                    {
                        placaEs.Tipo = t;
                        break;
                    }
                }

                // Mapear Orientacao
                OrientacaoPlaca orientacaoEnum;
                if (Enum.TryParse(orientacaoEs, out orientacaoEnum))
                {
                    placaEs.Orientacao = orientacaoEnum;
                }

                // Metadados do Comments se disponível
                if (comentario != null && comentario.StartsWith(PlacasService.PrefixoEtiqueta, StringComparison.Ordinal))
                {
                    string[] seg = comentario.Split(new[] { " | " }, StringSplitOptions.None);
                    if (seg.Length >= 7)
                    {
                        placaEs.Codigo = seg[5];
                        for (int i = 6; i < seg.Length; i++)
                        {
                            string s = seg[i];
                            if (s.StartsWith("placa ", StringComparison.Ordinal))
                            {
                                string resto = s.Substring("placa ".Length);
                                int sobra = resto.IndexOf(" (sobra de", StringComparison.Ordinal);
                                placaEs.Reaproveitada = sobra >= 0;
                                placaEs.PlacaOrigem = sobra >= 0 ? resto.Substring(0, sobra) : resto;
                            }
                            else if (s.StartsWith("medida ", StringComparison.Ordinal))
                            {
                                string[] lados = s.Substring("medida ".Length).Split('x');
                                double a;
                                double b;
                                if (lados.Length == 2 && Decimal(lados[0], out a) && Decimal(lados[1], out b))
                                {
                                    placaEs.Medida = new MedidaPlaca(a, b);
                                }
                            }
                            else if (s.StartsWith("orientacao ", StringComparison.Ordinal) && !placaEs.Orientacao.HasValue)
                            {
                                OrientacaoPlaca o;
                                if (Enum.TryParse(s.Substring("orientacao ".Length), out o)) placaEs.Orientacao = o;
                            }
                            else if (s.StartsWith("area ", StringComparison.Ordinal))
                            {
                                double area;
                                if (Decimal(s.Substring("area ".Length), out area)) placaEs.AreaLiquidaMm2 = area;
                            }
                        }
                    }
                }

                return placaEs;
            }
        }

        private static bool Numero(string texto, string prefixo, out int valor)
        {
            valor = 0;
            return texto.StartsWith(prefixo, StringComparison.Ordinal)
                && int.TryParse(texto.Substring(prefixo.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor);
        }

        private static bool Decimal(string texto, out double valor)
        {
            return double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }
    }
}