const { MongoClient } = require("mongodb");
const fs = require("fs");
const path = require("path");

async function run() {
    // Conexión a tu base de datos local
    const uri = "mongodb://127.0.0.1:27017";
    const client = new MongoClient(uri);

    try {
        await client.connect();
        const db = client.db("gc-deep-sea");
        const collection = db.collection("Costeras");

        // Leer el archivo generado por el paso anterior
        const dataPath = path.join(__dirname, "costeras_seed.json");
        const data = JSON.parse(fs.readFileSync(dataPath, "utf8"));

        // Limpiamos la colección por si ejecutamos esto más de una vez
        await collection.deleteMany({});
        console.log("Colección 'Costeras' reseteada.");

        // Insertamos los 31 polígonos
        const result = await collection.insertMany(data);
        console.log(`¡Éxito total! Se importaron ${result.insertedCount} polígonos jurisdiccionales en MongoDB.`);

    } catch (error) {
        console.error("Error durante la importación a Mongo:", error);
    } finally {
        await client.close();
    }
}

run();
