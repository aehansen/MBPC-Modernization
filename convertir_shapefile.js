const shapefile = require("shapefile");
const fs = require("fs");
const path = require("path");

async function run() {
  const shpPath = path.join(__dirname, "GeoSeed", "LIMITESJUR.shp");
  const dbfPath = path.join(__dirname, "GeoSeed", "LIMITESJUR.dbf");
  const outputPath = path.join(__dirname, "GeoSeed", "limites_geo.json");

  console.log(`Buscando Shapefile en: ${shpPath}`);
  
  if (!fs.existsSync(shpPath)) {
    console.error(`Error: No se encontró el archivo SHP en ${shpPath}`);
    process.exit(1);
  }

  const geojson = {
    type: "FeatureCollection",
    features: []
  };

  try {
    const source = await shapefile.open(shpPath, fs.existsSync(dbfPath) ? dbfPath : undefined, { encoding: "utf-8" });
    
    let count = 0;
    while (true) {
      const result = await source.read();
      if (result.done) break;

      geojson.features.push(result.value);

      if (count < 10) {
        console.log(`Registro #${count + 1} - Properties:`, JSON.stringify(result.value.properties, null, 2));
        count++;
      }
    }

    fs.writeFileSync(outputPath, JSON.stringify(geojson, null, 2), "utf8");
    console.log(`\n¡Conversión completada con éxito!`);
    console.log(`Se exportaron ${geojson.features.length} registros a: ${outputPath}`);
  } catch (error) {
    console.error("Error procesando el Shapefile:", error);
    process.exit(1);
  }
}

run();
