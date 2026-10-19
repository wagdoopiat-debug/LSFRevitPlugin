using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Registro de todas as lajes do documento: id, origem e configuracao, guardado uma unica
    /// vez no ProjectInformation via Extensible Storage. E a fonte de verdade para listar as
    /// lajes, reabrir a configuracao e regenerar mesmo depois de o piso de origem ser apagado.
    ///
    /// Schema proprio (GUID novo), nao reutiliza nem altera os schemas de parede/placas/paineis
    /// nem o de identidade por elemento (LSFPertencenciaLajes). Justificativa: e um registro
    /// por DOCUMENTO (nao por elemento), entao nao ha schema existente onde caiba.
    /// </summary>
    public static class RegistroLajesService
    {
        private static readonly Guid SchemaGuid = new Guid("2f7c9a10-6b3d-4e58-9a21-3c4d5e6f7081");
        private const string NomeSchema = "LSFRegistroLajes";
        private const string CampoRegistro = "Registro";

        /// <summary>Le todas as lajes registradas; lista vazia quando nao ha registro.</summary>
        public static List<RegistroLaje> Listar(Document doc)
        {
            var vazio = new List<RegistroLaje>();
            if (doc == null) return vazio;

            string texto = LerTexto(doc);
            List<RegistroLaje> lista;
            return RegistroLajesTexto.TentarDeserializar(texto, out lista) ? lista : vazio;
        }

        /// <summary>Maior LajeId em uso + 1 (comeca em 1).</summary>
        public static int ProximoLajeId(Document doc)
        {
            int maior = 0;
            foreach (RegistroLaje r in Listar(doc))
            {
                if (r.LajeId > maior) maior = r.LajeId;
            }

            return maior + 1;
        }

        /// <summary>
        /// Insere ou atualiza o registro da laje. Precisa ser chamado com uma Transaction aberta.
        /// </summary>
        public static void Salvar(Document doc, RegistroLaje registro)
        {
            if (doc == null || registro == null) return;

            List<RegistroLaje> lista = Listar(doc);
            lista.RemoveAll(r => r.LajeId == registro.LajeId);
            lista.Add(registro);
            lista.Sort((a, b) => a.LajeId.CompareTo(b.LajeId));

            EscreverTexto(doc, RegistroLajesTexto.Serializar(lista));
        }

        /// <summary>Remove o registro da laje. Precisa ser chamado com uma Transaction aberta.</summary>
        public static void Remover(Document doc, int lajeId)
        {
            if (doc == null) return;

            List<RegistroLaje> lista = Listar(doc);
            int removidos = lista.RemoveAll(r => r.LajeId == lajeId);
            if (removidos == 0) return;

            EscreverTexto(doc, RegistroLajesTexto.Serializar(lista));
        }

        private static string LerTexto(Document doc)
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return null;

            Element projeto = doc.ProjectInformation;
            if (projeto == null) return null;

            Entity entidade = projeto.GetEntity(schema);
            if (entidade == null || !entidade.IsValid()) return null;

            try
            {
                return entidade.Get<string>(CampoRegistro);
            }
            catch
            {
                return null;
            }
        }

        private static void EscreverTexto(Document doc, string texto)
        {
            Element projeto = doc.ProjectInformation;
            if (projeto == null) return;

            var entidade = new Entity(ObterOuCriarSchema());
            entidade.Set(CampoRegistro, texto ?? string.Empty);
            projeto.SetEntity(entidade);
        }

        private static Schema ObterOuCriarSchema()
        {
            Schema existente = Schema.Lookup(SchemaGuid);
            if (existente != null) return existente;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(NomeSchema);
            builder.SetVendorId("LSFModeler");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(CampoRegistro, typeof(string));
            return builder.Finish();
        }
    }
}
