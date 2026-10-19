using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Registro de uma laje gerada no projeto: o id da laje, a origem do contorno e a
    /// configuracao completa (com o contorno). E o que permite listar as lajes do documento,
    /// reabrir a configuracao para editar e regenerar mesmo depois de o elemento de origem
    /// (piso) ser apagado. Puro (sem Revit).
    /// </summary>
    public sealed class RegistroLaje
    {
        public int LajeId { get; set; }

        public TipoOrigemContorno OrigemTipo { get; set; }

        /// <summary>Id do elemento de origem (piso) quando houver; -1 quando nao ha.</summary>
        public int OrigemElementoId { get; set; } = -1;

        public ParametrosLaje Parametros { get; set; }
    }

    /// <summary>
    /// Codec do registro de lajes do documento. Cada laje e um bloco de linhas "chave=valor",
    /// terminado por uma linha "###"; os blocos sao separados por quebra de linha. O bloco tem
    /// tres chaves de registro (lajeId, origemTipo, origemId) e, abaixo, a configuracao completa
    /// serializada por ConfiguracaoLajeTexto. Puro (sem Revit).
    /// </summary>
    public static class RegistroLajesTexto
    {
        public const string SeparadorBloco = "###";

        public static string Serializar(IEnumerable<RegistroLaje> registros)
        {
            if (registros == null) return string.Empty;

            var blocos = new List<string>();
            foreach (RegistroLaje r in registros)
            {
                if (r == null || r.Parametros == null) continue;

                var linhas = new List<string>
                {
                    "lajeId=" + r.LajeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "origemTipo=" + r.OrigemTipo,
                    "origemId=" + r.OrigemElementoId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ConfiguracaoLajeTexto.Serializar(r.Parametros),
                    SeparadorBloco
                };

                blocos.Add(string.Join("\n", linhas));
            }

            return string.Join("\n", blocos);
        }

        /// <summary>
        /// Le o registro. Devolve false quando nao ha nenhuma laje valida; registros individuais
        /// invalidos sao simplesmente ignorados (nao derrubam a leitura dos demais).
        /// </summary>
        public static bool TentarDeserializar(string texto, out List<RegistroLaje> registros)
        {
            var lista = new List<RegistroLaje>();
            registros = lista;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            string[] linhas = texto.Replace("\r\n", "\n").Split('\n');

            int lajeId = -1;
            TipoOrigemContorno origem = TipoOrigemContorno.BordasSelecionadas;
            int origemId = -1;
            bool temOrigemId = false;
            var configLinhas = new List<string>();

            void FecharBloco()
            {
                if (lajeId < 0) return;

                ParametrosLaje parametros;
                if (!ConfiguracaoLajeTexto.TentarDeserializar(string.Join("\n", configLinhas), out parametros))
                {
                    return;
                }

                lista.Add(new RegistroLaje
                {
                    LajeId = lajeId,
                    OrigemTipo = origem,
                    OrigemElementoId = temOrigemId ? origemId : -1,
                    Parametros = parametros
                });
            }

            foreach (string linhaBruta in linhas)
            {
                string linha = linhaBruta.TrimEnd('\r');

                if (linha == SeparadorBloco)
                {
                    FecharBloco();
                    lajeId = -1;
                    origem = TipoOrigemContorno.BordasSelecionadas;
                    origemId = -1;
                    temOrigemId = false;
                    configLinhas.Clear();
                    continue;
                }

                if (linha.Length == 0) continue;

                int igual = linha.IndexOf('=');
                if (igual <= 0) continue;

                string chave = linha.Substring(0, igual);
                string valor = linha.Substring(igual + 1);

                switch (chave)
                {
                    case "lajeId":
                        int.TryParse(valor, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out lajeId);
                        break;
                    case "origemTipo":
                        Enum.TryParse(valor, out origem);
                        break;
                    case "origemId":
                        temOrigemId = int.TryParse(valor, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out origemId);
                        break;
                    default:
                        configLinhas.Add(linha);
                        break;
                }
            }

            FecharBloco();
            return lista.Count > 0;
        }
    }
}
