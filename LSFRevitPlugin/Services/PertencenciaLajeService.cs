using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Guarda a identidade de um elemento gerado da laje -- laje, tipo, papel e numero -- no
    /// proprio elemento, via Extensible Storage. E a fonte de verdade dessa relacao e o que
    /// permite regerar uma laje sem duplicar elementos.
    ///
    /// O texto no campo Comentarios continua sendo gravado com os mesmos valores (formato legado),
    /// para o usuario ler no modelo. Os dois canais sao gravados juntos, na mesma chamada.
    ///
    /// Schema separado do LSFPertencenciaPaineis e do LSFPertencenciaPlacas, com GUID proprio,
    /// para nao colidir com a identidade da parede. Nao reutiliza nenhum GUID existente.
    /// </summary>
    public static class PertencenciaLajeService
    {
        private static readonly Guid SchemaGuid = new Guid("a7bdd95e-e83e-4f7b-83eb-56c897218bdd");
        private const string NomeSchema = "LSFPertencenciaLajes";
        private const string CampoLajeId = "LajeId";
        private const string CampoTipo = "Tipo";
        private const string CampoPapel = "Papel";
        private const string CampoNumero = "Numero";

        private static Schema ObterOuCriarSchema()
        {
            Schema existente = Schema.Lookup(SchemaGuid);
            if (existente != null) return existente;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(NomeSchema);
            builder.SetVendorId("LSFModeler");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(CampoLajeId, typeof(int));
            builder.AddSimpleField(CampoTipo, typeof(string));
            builder.AddSimpleField(CampoPapel, typeof(string));
            builder.AddSimpleField(CampoNumero, typeof(int));
            return builder.Finish();
        }

        /// <summary>
        /// Grava a identidade no elemento. Precisa ser chamado com uma Transaction aberta.
        /// </summary>
        public static void Gravar(Element elemento, int lajeId, string tipo, string papel, int numero)
        {
            if (elemento == null) return;

            var entidade = new Entity(ObterOuCriarSchema());
            entidade.Set(CampoLajeId, lajeId);
            entidade.Set(CampoTipo, tipo ?? string.Empty);
            entidade.Set(CampoPapel, papel ?? string.Empty);
            entidade.Set(CampoNumero, numero);
            elemento.SetEntity(entidade);
        }

        /// <summary>
        /// Le a identidade gravada no elemento. Devolve false quando o elemento nao tem esse
        /// schema -- acontece com elementos gerados antes desta etapa existir ou de outra origem.
        /// </summary>
        public static bool TentarLer(Element elemento, out IdentidadeLaje identidade)
        {
            identidade = null;
            if (elemento == null) return false;

            // Lookup, nao ObterOuCriar: a leitura nao pode criar schema (precisaria de transacao).
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return false;

            Entity entidade = elemento.GetEntity(schema);
            if (entidade == null || !entidade.IsValid()) return false;

            try
            {
                TipoLaje tipo;
                if (!Enum.TryParse(entidade.Get<string>(CampoTipo), out tipo)) tipo = TipoLaje.Umida;

                PapelElementoLaje papel;
                if (!Enum.TryParse(entidade.Get<string>(CampoPapel), out papel)) papel = PapelElementoLaje.Viga;

                identidade = new IdentidadeLaje
                {
                    LajeId = entidade.Get<int>(CampoLajeId),
                    Tipo = tipo,
                    Papel = papel,
                    Numero = entidade.Get<int>(CampoNumero)
                };
                return true;
            }
            catch
            {
                identidade = null;
                return false;
            }
        }
    }
}
