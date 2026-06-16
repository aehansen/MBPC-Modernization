using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Mbpc.Api.Services
{
    public sealed class OracleDynamicParameters : SqlMapper.IDynamicParameters
    {
        private readonly List<OracleParameterInfo> _params = new();
        private readonly List<OracleParameter> _oracleParameters = new();

        private sealed class OracleParameterInfo
        {
            public required string             Name      { get; init; }
            public          object?            Value     { get; init; }
            public required OracleDbType       DbType    { get; init; }
            public required ParameterDirection Direction { get; init; }
        }

        public void Add(string name, object? value, OracleDbType dbType,
                        ParameterDirection direction = ParameterDirection.Input)
            => _params.Add(new OracleParameterInfo
            {
                Name      = name,
                Value     = value,
                DbType    = dbType,
                Direction = direction
            });

        public void Add(string name, OracleDbType dbType, ParameterDirection direction)
            => _params.Add(new OracleParameterInfo
            {
                Name      = name,
                Value     = null,
                DbType    = dbType,
                Direction = direction
            });

        public T Get<T>(string name)
        {
            var parameterName = name.StartsWith("p_") ? name : "p_" + name;
            var p = _oracleParameters.FirstOrDefault(x => 
                x.ParameterName.Equals(name, StringComparison.OrdinalIgnoreCase) || 
                x.ParameterName.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

            if (p == null)
                throw new KeyNotFoundException($"Parameter '{name}' not found.");

            var val = p.Value;
            if (val == DBNull.Value || val == null)
                return default!;

            if (val is OracleDecimal dec)
            {
                return (T)Convert.ChangeType(dec.Value, typeof(T));
            }

            return (T)Convert.ChangeType(val, typeof(T));
        }

        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            if (command is not OracleCommand oracleCmd)
                throw new InvalidOperationException(
                    "OracleDynamicParameters solo puede usarse con OracleCommand.");

            _oracleParameters.Clear();
            foreach (var p in _params)
            {
                var oracleParam = oracleCmd.Parameters.Add(p.Name, p.DbType);
                oracleParam.Direction = p.Direction;

                if (p.Value is not null)
                    oracleParam.Value = p.Value;

                _oracleParameters.Add(oracleParam);
            }
        }
    }
}
