export interface MetadataField {
  name: string;
  label: string;
  type: string;
}

export interface MetadataJoin {
  targetEntity: string;
  joinTable: string;
  localKey: string;
  foreignKey: string;
}

export interface MetadataEntity {
  name: string;
  fields: MetadataField[];
  joins?: MetadataJoin[];
}

export interface QueryFilter {
  campo: string;
  operador: string;
  valor: string;
}

export interface QueryRequest {
  entidadPrincipal: string;
  columnas: string[];
  filtros: QueryFilter[];
}

export interface QueryResult {
  columnas: string[];
  filas: Record<string, any>[];
}
