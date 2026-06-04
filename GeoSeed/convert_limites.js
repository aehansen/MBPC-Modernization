const shapefile = require("shapefile");
const fs = require("fs");
const path = require("path");

async function convertirShapefile() {
    const shpPath = path.join(__dirname, "LIMITESJUR.shp");
    const dbfPath = path.join(__dirname, "LIMITESJUR.dbf");
    const outputPath = path.join(__dirname, "costeras_seed.json");

    const docs = [];
    let contadorId = 1;

    try {
        console.log("Iniciando la lectura del Shapefile...");
        const source = await shapefile.open(shpPath, dbfPath, { encoding: "utf-8" });

        while (true) {
            const result = await source.read();
            if (result.done) break;

            const elemento = result.value;
            const costeraId = elemento.properties.ID_JUR ? Number(elemento.properties.ID_JUR) : contadorId;

            // Formato directo para la colección Costeras de MongoDB
            const doc = {
                costeraId: costeraId,
                nombre: elemento.properties.NOMBRE || `Jurisdicción Marina ${costeraId}`,
                geometria: {
                    type: elemento.geometry.type,
                    coordinates: elemento.geometry.coordinates
                }
            };

            docs.push(doc);
            contadorId++;
        }

        fs.writeFileSync(outputPath, JSON.stringify(docs, null, 2), "utf-8");
        console.log(`Conversión exitosa. ${docs.length} polígonos listos para MongoDB.`);
    } catch (error) {
        console.error("Error crítico durante el procesamiento GIS:", error);
        process.exit(1);
    }
}

convertirShapefile();
