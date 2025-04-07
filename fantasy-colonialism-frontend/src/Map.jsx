import { useEffect, useState } from 'react'
import './App.css'

function Map() {
    const [svgContent, setSvgContent] = useState(null);

    useEffect(() => {
        const url = 'svg-test.svg';

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
    };

    return (
        <div>
            {svgContent && (
                <div
                    onClick={handleSvgClick}
                    dangerouslySetInnerHTML={{ __html: svgContent }}
                />
            )}
        </div>
    );
}

export default Map