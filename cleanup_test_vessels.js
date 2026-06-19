import { MongoClient } from 'mongodb';

const uri = 'mongodb://localhost:27017';
const dbName = 'gc-deep-sea';

async function run() {
  const client = new MongoClient(uri);

  try {
    await client.connect();
    console.log('Conectado exitosamente a MongoDB');
    const db = client.db(dbName);
    const collectionPos = db.collection('last_mbpc');
    const collectionDet = db.collection('details_mbpc');

    const testTravelIds = [9900001, 9900002, 9900003];

    for (const travelId of testTravelIds) {
      const posRes = await collectionPos.deleteOne({ TravelId: travelId });
      const detRes = await collectionDet.deleteOne({ IdViaje: travelId });

      if (posRes.deletedCount > 0 || detRes.deletedCount > 0) {
        console.log(`Eliminado buque test con TravelId: ${travelId}`);
      }
    }

    console.log('\n¡Limpieza de buques de prueba completada con éxito!');
  } catch (err) {
    console.error('Error al realizar la limpieza de datos de prueba:', err);
  } finally {
    await client.close();
  }
}

run();
