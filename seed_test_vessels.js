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

    const testVessels = [
      {
        travelId: 9900001,
        name: 'DON BENJAMIN TEST',
        mmsi: '9990001',
        imo: 9900001,
        lat: -33.0095,
        lng: -58.5085,
        costeraId: 425,
        origin: 'Gualeguaychú',
        destination: 'Buenos Aires'
      },
      {
        travelId: 9900002,
        name: 'SAN PEDRO EXPLORER',
        mmsi: '9990002',
        imo: 9900002,
        lat: -33.6750,
        lng: -59.6636,
        costeraId: 421,
        origin: 'San Pedro',
        destination: 'Zárate'
      },
      {
        travelId: 9900003,
        name: 'BUENOS AIRES EXPRESS',
        mmsi: '9990003',
        imo: 9900003,
        lat: -34.5807,
        lng: -58.3762,
        costeraId: 474,
        origin: 'Buenos Aires',
        destination: 'Montevideo'
      }
    ];

    for (const v of testVessels) {
      // Eliminar si ya existe para evitar duplicados
      await collectionPos.deleteOne({ TravelId: v.travelId });
      await collectionDet.deleteOne({ IdViaje: v.travelId });

      // Insertar en last_mbpc
      const posDoc = {
        TravelId: v.travelId,
        VesselName: v.name,
        MMSI: v.mmsi,
        IMO: v.imo,
        Latitude: v.lat,
        Longitude: v.lng,
        NavegationStatusDesc: 'Navegando',
        SpeedOverGroud: 12.5,
        CourseOverGround: 180.0,
        msgTime: new Date(),
        Origin: v.origin,
        Destination: v.destination,
        CosteraId: v.costeraId,
        RequiereTransferencia: false,
        CosteraIdPendiente: null,
        location: {
          geo: {
            type: 'Point',
            coordinates: [v.lng, v.lat]
          }
        }
      };
      await collectionPos.insertOne(posDoc);

      // Insertar en details_mbpc
      const detDoc = {
        IdViaje: v.travelId,
        VesselName: v.name,
        Origin: v.origin,
        Destination: v.destination,
        CosteraId: v.costeraId,
        ETAPAS: [
          {
            ETAPA_ID: 1,
            FECHA_INICIO: new Date(),
            FECHA_FIN: null,
            BARCAZAS: []
          }
        ],
        Eventos: []
      };
      await collectionDet.insertOne(detDoc);

      console.log(`Buque test insertado: ${v.name} (TravelId: ${v.travelId}) en Costera ${v.costeraId}`);
    }

    console.log('\n¡Sembrado de buques de prueba finalizado con éxito!');
  } catch (err) {
    console.error('Error al sembrar datos de prueba:', err);
  } finally {
    await client.close();
  }
}

run();
