import { useEffect, useState } from 'react'
import './App.css'

function Map() {
    const [svgContent, setSvgContent] = useState(null);

    useEffect(() => {
        const url = 'sf-continent-3.svg';

        fetch(url)
            .then(response => response.text())
            .then(svgText => {
                console.log(svgText);
                console.log('\n----------------------------------\n');
                const parser = new DOMParser();
                const svgDoc = parser.parseFromString(svgText, 'image/svg+xml');
                const svgElement = svgDoc.documentElement;

                svgElement.setAttribute('class', 'svglayer');
                setSvgContent(svgText);

            })
            .catch(error => console.error('Error loading SVG:', error));
    }, []);



    const handleSvgClick = (e) => {
        console.log(e.target.id);
        console.log(e.target.getAttribute('fill'));
        e.target.setAttribute('fill','red')
    };

    const handleSvgHover = (e) => {
        console.log(e.target.id);
        if(e.target.getAttribute('fill') !== 'red'){
            e.target.setAttribute('fill','grey')
        }
    };

    const handleSvgUnhover = (e) => {
        console.log(e.target.id);
        if(e.target.getAttribute('fill') !== 'red'){
            e.target.setAttribute('fill','white')
        }
    };


    return (
        <div>
            {svgContent && (
                <div
                    onClick={handleSvgClick}
                    onMouseOver={handleSvgHover}
                    onMouseOut={handleSvgUnhover}
                    dangerouslySetInnerHTML={{ __html: svgContent }}
                />
            )}
        </div>
    );
}

export default Map