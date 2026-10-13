// Created by Samuel Teixeira Parchao
// Last modified: 13/02/2026

namespace WeatherApp.Elements;

/// <summary>
/// Provides HTML content for rendering the weather map view inside WebView2.
/// </summary>
public class WindowElements
{
    /// <summary>
    /// Gets the HTML used by the map tab.
    /// </summary>
    /// <returns>Leaflet-based map HTML including world tiles, temperature overlay, and tracked city markers.</returns>
    public static string GetMapHtml()
    {
        return """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width,initial-scale=1.0">
            <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
            <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
            <style>
            *{margin:0;padding:0}
            body{background:#cfd8e7;overflow:hidden}
            #map{width:100vw;height:100vh}
            .leaflet-control-attribution{display:none!important}
            .leaflet-control-zoom a{
                background:rgba(255,255,255,0.92)!important;
                color:#1a2b49!important;
                border-color:rgba(26,43,73,0.15)!important;
            }
            .leaflet-control-zoom a:hover{background:#fff!important;color:#0d1a30!important}
            .legend{
                background:rgba(255,255,255,0.92)!important;
                border:0.5px solid rgba(31,48,76,0.18);
                border-radius:14px;padding:12px 15px;
                color:#223756;
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
                box-shadow:0 8px 22px rgba(21,35,58,0.15);
            }
            .legend h4{
                margin:0 0 8px 0;font-size:10px;font-weight:700;
                text-transform:uppercase;letter-spacing:1.2px;opacity:0.7;
            }
            .grad-bar{
                width:170px;height:7px;border-radius:3px;
                background:linear-gradient(to right,#313695,#4575b4,#91bfdb,#e0f3f8,#fee090,#fdae61,#f46d43,#d73027,#a50026);
                margin-bottom:6px;
            }
            .grad-labels{display:flex;justify-content:space-between;font-size:10px;opacity:0.75}
            .city-marker{position:relative;display:flex;flex-direction:column;align-items:center;transform:translateY(-8px)}
            .city-marker .temp-pill{
                color:#fff;padding:4px 10px;border-radius:12px;font-size:13px;font-weight:700;
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
                text-shadow:0 1px 2px rgba(0,0,0,0.35);
                box-shadow:0 4px 12px rgba(0,0,0,0.25);
            }
            .city-marker .city-label{
                margin-top:3px;font-size:11px;font-weight:600;color:#203252;
                text-shadow:0 1px 1px rgba(255,255,255,0.7);
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
                white-space:nowrap;
            }
            .city-marker.favorite .temp-pill{outline:2px solid rgba(255,255,255,0.96)}
            .city-marker.active .temp-pill{transform:scale(1.08);box-shadow:0 7px 15px rgba(11,33,70,0.36)}
            .leaflet-pane.leaflet-overlay-pane canvas{mix-blend-mode:multiply}
            </style>
            </head>
            <body>
            <div id="map"></div>
            <script>
            const map=L.map('map',{center:[22,2],zoom:2,zoomControl:false,attributionControl:false,minZoom:2,maxZoom:8});
            L.tileLayer('https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png',{subdomains:'abc',maxZoom:19}).addTo(map);
            L.control.zoom({position:'topright'}).addTo(map);

            const PI=Math.PI/180;
            const colorScale=[
                {t:-25,r:49,g:54,b:149},{t:-10,r:69,g:117,b:180},
                {t:0,r:116,g:173,b:209},{t:5,r:171,g:217,b:233},
                {t:10,r:224,g:243,b:248},{t:15,r:254,g:224,b:144},
                {t:20,r:253,g:174,b:97},{t:28,r:244,g:109,b:67},
                {t:35,r:215,g:48,b:39},{t:42,r:165,g:0,b:38}
            ];

            function gaussian(lat,lng,centerLat,centerLng,spreadLat,spreadLng){
                const a=(lat-centerLat)/spreadLat;
                const b=(lng-centerLng)/spreadLng;
                return Math.exp(-(a*a+b*b));
            }

            function estimateTemperature(lat,lng){
                let base=30*Math.cos(lat*PI*1.1)-2;
                base+=gaussian(lat,lng,25,20,12,35)*10;
                base+=gaussian(lat,lng,24,48,8,12)*8;
                base+=gaussian(lat,lng,22,78,12,15)*5;
                base+=gaussian(lat,lng,5,110,14,20)*4;
                base+=gaussian(lat,lng,-25,135,8,12)*5;
                base+=gaussian(lat,lng,0,-60,18,20)*3;
                base+=gaussian(lat,lng,10,25,12,15)*4;
                base+=gaussian(lat,lng,34,-112,8,12)*4;
                base-=gaussian(lat,lng,62,95,12,30)*18;
                base-=gaussian(lat,lng,65,-95,12,25)*15;
                base-=gaussian(lat,lng,72,-42,10,12)*20;
                base-=gaussian(lat,lng,78,20,8,30)*12;
                base-=gaussian(lat,lng,-80,0,10,60)*30;
                base-=gaussian(lat,lng,62,-150,10,15)*12;
                base+=Math.sin(lat*.17+2.3)*Math.cos(lng*.13+1.7)*2;
                base+=Math.sin(lat*.31-1.1)*Math.cos(lng*.23+.5)*1.5;
                return base;
            }

            function temperatureColorRamp(temp){
                if(temp<=colorScale[0].t){return colorScale[0];}
                if(temp>=colorScale[colorScale.length-1].t){return colorScale[colorScale.length-1];}
                for(let i=0;i<colorScale.length-1;i++){
                    const low=colorScale[i];
                    const high=colorScale[i+1];
                    if(temp<=high.t){
                        const factor=(temp-low.t)/(high.t-low.t);
                        return {
                            r:(low.r+factor*(high.r-low.r))|0,
                            g:(low.g+factor*(high.g-low.g))|0,
                            b:(low.b+factor*(high.b-low.b))|0
                        };
                    }
                }
                return colorScale[0];
            }

            const TemperatureLayer=L.GridLayer.extend({
                createTile:function(coords){
                    const tileSize=this.getTileSize();
                    const downsample=2;
                    const width=(tileSize.x/downsample)|0;
                    const height=(tileSize.y/downsample)|0;

                    const source=document.createElement('canvas');
                    source.width=width;
                    source.height=height;
                    const context=source.getContext('2d');
                    const imageData=context.createImageData(width,height);
                    const pixels=imageData.data;

                    const northWest=this._map.unproject([coords.x*tileSize.x,coords.y*tileSize.y],coords.z);
                    const southEast=this._map.unproject([(coords.x+1)*tileSize.x,(coords.y+1)*tileSize.y],coords.z);
                    const latStart=northWest.lat;
                    const latDelta=(southEast.lat-latStart)/height;
                    const lngStart=northWest.lng;
                    const lngDelta=(southEast.lng-lngStart)/width;

                    for(let y=0;y<height;y++){
                        const latitude=latStart+y*latDelta;
                        for(let x=0;x<width;x++){
                            const longitude=lngStart+x*lngDelta;
                            const color=temperatureColorRamp(estimateTemperature(latitude,longitude));
                            const pixelIndex=(y*width+x)*4;
                            pixels[pixelIndex]=color.r;
                            pixels[pixelIndex+1]=color.g;
                            pixels[pixelIndex+2]=color.b;
                            pixels[pixelIndex+3]=150;
                        }
                    }

                    context.putImageData(imageData,0,0);

                    const output=document.createElement('canvas');
                    output.width=tileSize.x;
                    output.height=tileSize.y;
                    const outputContext=output.getContext('2d');
                    outputContext.imageSmoothingEnabled=true;
                    outputContext.imageSmoothingQuality='high';
                    outputContext.drawImage(source,0,0,tileSize.x,tileSize.y);
                    return output;
                }
            });

            new TemperatureLayer({opacity:0.62}).addTo(map);

            const markerLayer=L.layerGroup().addTo(map);

            function temperatureColor(temp){
                if(temp>=38){return '#a50026';}
                if(temp>=31){return '#d73027';}
                if(temp>=24){return '#f46d43';}
                if(temp>=18){return '#fdae61';}
                if(temp>=10){return '#fee08b';}
                if(temp>=2){return '#74add1';}
                return '#4575b4';
            }

            function markerHtml(city){
                const color=temperatureColor(city.temperature);
                const favoriteClass=city.isFavorite?' favorite':'';
                const activeClass=city.isActive?' active':'';
                const safeName=city.name??'City';
                return '<div class="city-marker'+favoriteClass+activeClass+'">'
                    +'<div class="temp-pill" style="background:'+color+'">'+Math.round(city.temperature)+'\u00b0</div>'
                    +'<div class="city-label">'+safeName+'</div></div>';
            }

            window.setTrackedCities=function(cities){
                markerLayer.clearLayers();
                if(!Array.isArray(cities)||cities.length===0){return;}

                let activeCity=null;
                const bounds=[];
                cities.forEach(function(city){
                    if(typeof city.latitude!=='number'||typeof city.longitude!=='number'){return;}
                    if(typeof city.temperature!=='number'){city.temperature=0;}

                    const marker=L.marker([city.latitude,city.longitude],{
                        icon:L.divIcon({
                            className:'',
                            html:markerHtml(city),
                            iconSize:[112,48],
                            iconAnchor:[56,24]
                        })
                    });

                    marker.bindPopup((city.name??'City')+'<br/>'+Math.round(city.temperature)+'\u00b0C');
                    marker.addTo(markerLayer);
                    bounds.push([city.latitude,city.longitude]);

                    if(city.isActive){activeCity=city;}
                });

                if(activeCity){
                    map.flyTo([activeCity.latitude,activeCity.longitude],Math.max(map.getZoom(),4),{animate:true,duration:0.7});
                }else if(bounds.length>1){
                    map.fitBounds(bounds,{padding:[30,30]});
                }
            };

            const legend=L.control({position:'bottomleft'});
            legend.onAdd=function(){
                const node=L.DomUtil.create('div','legend');
                node.innerHTML='<h4>Temperature Overlay</h4><div class="grad-bar"></div><div class="grad-labels"><span>-10\u00b0</span><span>10\u00b0</span><span>25\u00b0</span><span>40\u00b0+</span></div>';
                return node;
            };
            legend.addTo(map);
            </script>
            </body>
            </html>
            """;
    }
}
