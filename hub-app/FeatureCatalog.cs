using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WolfHub
{
    internal sealed class FeatureInfo
    {
        public string Id;
        public string Name;
        public string Changes;
        public string Dependencies;
        public string Permission;
        public string Reversible;
        public string Scope;
    }

    internal sealed class ModuleInfo
    {
        public string Id;
        public string Name;
        public string Purpose;
        public string Dependencies;
        public string Status;
    }

    internal sealed class FeatureCatalog
    {
        private readonly Dictionary<string, FeatureInfo> features = new Dictionary<string, FeatureInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModuleInfo> modules = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);

        public static FeatureCatalog Load(string featuresJson, string modulesJson)
        {
            var result = new FeatureCatalog();
            var serializer = new JavaScriptSerializer();
            var featureRoot = serializer.Deserialize<Dictionary<string, object>>(featuresJson);
            var featureRows = featureRoot != null && featureRoot.ContainsKey("features") ? featureRoot["features"] as System.Collections.ArrayList : null;
            if (featureRows == null) throw new InvalidOperationException("config/features.json has no features array.");
            foreach (Dictionary<string, object> row in featureRows)
            {
                var info = new FeatureInfo
                {
                    Id = Read(row, "id"), Name = Read(row, "name"), Changes = Read(row, "changes"), Dependencies = Read(row, "dependencies"),
                    Permission = Read(row, "permission"), Reversible = Read(row, "reversible"), Scope = Read(row, "scope")
                };
                if (String.IsNullOrWhiteSpace(info.Id) || String.IsNullOrWhiteSpace(info.Changes) || String.IsNullOrWhiteSpace(info.Dependencies) || String.IsNullOrWhiteSpace(info.Permission) || String.IsNullOrWhiteSpace(info.Reversible))
                    throw new InvalidOperationException("A feature entry is missing its id or action context.");
                if (result.features.ContainsKey(info.Id)) throw new InvalidOperationException("Duplicate feature id: " + info.Id);
                result.features.Add(info.Id, info);
            }

            var moduleRoot = serializer.Deserialize<Dictionary<string, object>>(modulesJson);
            var moduleRows = moduleRoot != null && moduleRoot.ContainsKey("modules") ? moduleRoot["modules"] as System.Collections.ArrayList : null;
            if (moduleRows == null) throw new InvalidOperationException("config/modules.json has no modules array.");
            foreach (Dictionary<string, object> row in moduleRows)
            {
                var info = new ModuleInfo { Id = Read(row, "id"), Name = Read(row, "name"), Purpose = Read(row, "purpose"), Dependencies = Read(row, "dependencies"), Status = Read(row, "status") };
                if (String.IsNullOrWhiteSpace(info.Id) || String.IsNullOrWhiteSpace(info.Name)) throw new InvalidOperationException("A module entry is missing its id or name.");
                if (result.modules.ContainsKey(info.Id)) throw new InvalidOperationException("Duplicate module id: " + info.Id);
                result.modules.Add(info.Id, info);
            }
            return result;
        }

        public FeatureInfo GetFeature(string id)
        {
            FeatureInfo value;
            if (!features.TryGetValue(id ?? "", out value)) throw new InvalidOperationException("Action metadata is missing for feature: " + id);
            return value;
        }

        public ModuleInfo GetModule(string id)
        {
            ModuleInfo value;
            return modules.TryGetValue(id ?? "", out value) ? value : null;
        }

        private static string Read(Dictionary<string, object> row, string key)
        {
            object value;
            if (row == null || !row.TryGetValue(key, out value) || value == null) return "";
            var list = value as System.Collections.ArrayList;
            if (list != null) return String.Join("; ", list.ToArray());
            return value.ToString();
        }
    }
}
